using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using EmuClean.Engine;

namespace EmuClean.Engine
{
    /// <summary>
    /// The bridge called from JS (chrome.webview.postMessage). Mirrors the
    /// pywebview api surface used by ui/js/app.js.
    /// </summary>
    public class Api
    {
        private readonly List<string> _log = new List<string>();
        private readonly object _lock = new object();
        private List<DetectedEmu> _detected = new List<DetectedEmu>();
        private readonly Dictionary<string, AdbManager> _adb =
            new Dictionary<string, AdbManager>();
        private DeviceManager _device;

        private void Log(string msg)
        {
            lock (_lock) _log.Add(msg);
        }

        public List<string> GetLog()
        {
            lock (_lock)
            {
                return _log.Skip(Math.Max(0, _log.Count - 400)).ToList();
            }
        }

        public object Invoke(string method, JArray args, Form form)
        {
            switch (method)
            {
                case "detect": return Detect();
                case "execute_clean": return ExecuteClean(args[0], args.Count > 1 && (bool?)args[1] == true);
                case "adb_status": return AdbStatus((string)args[0]);
                case "adb_connect": return AdbConnect((string)args[0],
                    args.Count > 1 && args[1] != null && args[1].Type != JTokenType.Null
                        ? (string)args[1] : null);
                case "adb_packages": return AdbPackages((string)args[0]);
                case "adb_uninstall": return AdbUninstall((string)args[0],
                    args[1].Select(t => (string)t).ToList());
                case "optimizer_catalog": return OptimizerCatalog();
                case "apply_optimizer": return ApplyOptimizer(
                    args[0].Select(t => (string)t).ToList(),
                    args[1].Select(t => (string)t).ToList(),
                    args.Count > 2 && args[2] != null && args[2].Type != JTokenType.Null
                        ? (string)args[2] : null);
                case "running_targets": return Optimizer.RunningTargets();
                case "device_status": return DeviceStatus();
                case "device_connect": return DeviceConnect(args.Count > 0 && args[0] != null
                    && args[0].Type != JTokenType.Null ? (string)args[0] : null);
                case "device_reconnect": return DeviceReconnect();
                case "device_info": return DeviceInfo();
                case "device_packages": return DevicePackages(args.Count > 0 && (bool?)args[0] == true);
                case "device_uninstall": return DeviceUninstall(
                    args[0].Select(t => (string)t).ToList());
                case "device_restore": return DeviceRestore((string)args[0]);
                case "device_force_stop_all": return DeviceForceStop();
                case "device_deny_notifications": return DeviceDenyNotif();
                case "device_ls": return DeviceLs((string)args[0]);
                case "device_pull": return DevicePull((string)args[0],
                    args.Count > 1 && args[1] != null && args[1].Type != JTokenType.Null
                        ? (string)args[1] : null);
                case "device_push": return DevicePush((string)args[0], (string)args[1]);
                case "device_quick_pull": return DeviceQuickPull((string)args[0]);
                case "device_reboot": return DeviceReboot(args.Count > 0 && args[0] != null
                    && args[0].Type != JTokenType.Null ? (string)args[0] : "system");
                case "get_log": return GetLog();
                case "close_window":
                    form.BeginInvoke((Action)(() => form.Close()));
                    return new object();
                case "__window_min":
                    form.BeginInvoke((Action)(() => form.WindowState = FormWindowState.Minimized));
                    return new object();
                default:
                    throw new ArgumentException("unknown method: " + method);
            }
        }

        // ------------------------------------------------ detect
        private object Detect()
        {
            _detected = Detector.Detect(true);
            var outp = new List<object>();
            foreach (var emu in _detected)
            {
                var rows = Cleaner.ScanEmulator(emu);
                long junk = rows.Where(r => r.Auto).Sum(r => r.Size);
                outp.Add(new
                {
                    id = emu.Id,
                    name = emu.Name,
                    kind = emu.Kind,
                    emoji = EmojiOf(emu.Id),
                    installed = emu.Installed,
                    running = emu.Running,
                    install_root = emu.InstallRoot,
                    data_root = emu.DataRoot,
                    junk_size = junk,
                    junk_human = Cleaner.Human(junk),
                    rules = rows.Select(r => new
                    {
                        category = r.Category,
                        label = r.Label,
                        note = r.Note,
                        paths = r.Paths,
                        size = r.Size,
                        files = r.Files,
                        auto = r.Auto,
                        locked = r.Locked,
                    }).ToList(),
                });
            }
            return new { emulators = outp, leftovers = Cleaner.FindLeftovers(), version = "1.1.0" };
        }

        private static string EmojiOf(string id)
        {
            var map = new Dictionary<string, string>
            {
                ["bluestacks5"] = "🔷", ["bluestacks4"] = "🔹", ["ldplayer"] = "🐉",
                ["nox"] = "🌑", ["memu"] = "🟣", ["gameloop"] = "🎮", ["mumu"] = "🟢",
                ["koplayer"] = "🟠", ["genymotion"] = "🤖", ["dolphin"] = "🐬",
                ["pcsx2"] = "🎮", ["rpcs3"] = "💠", ["yuzu"] = "🍊", ["suyu"] = "🧡",
                ["ryujinx"] = "🔺", ["citralime"] = "🍋", ["ppsspp"] = "🟦",
                ["cemu"] = "🟩", ["xenia"] = "🟡", ["duckstation"] = "🦆",
                ["retroarch"] = "🕹️", ["mgba"] = "🎮", ["project64"] = "🟪",
                ["vita3k"] = "✨", ["xemu"] = "🟢",
            };
            return map.ContainsKey(id) ? map[id] : "🎮";
        }

        // ------------------------------------------------ clean
        private object ExecuteClean(JToken selections, bool permanent)
        {
            var plan = new List<CleanItem>();
            foreach (var sel in (JArray)selections)
            {
                var emu = (string)sel["emu"];
                var label = (string)sel["label"];
                foreach (var p in (JArray)sel["paths"])
                    plan.Add(new CleanItem { Emu = emu, Label = label, Path = (string)p });
            }
            Log("clean started: " + plan.Count + " target(s), " +
                (permanent ? "permanent" : "recycle bin") + " mode");
            var res = Cleaner.Execute(plan, permanent, Log);
            Log(GetLogLast());
            return new
            {
                deleted = res.Deleted,
                freed = res.Freed,
                failed = res.Failed,
                errors = res.Errors,
                log = GetLog(),
            };
        }

        private string GetLogLast() { return _log.Count > 0 ? _log[_log.Count - 1] : ""; }

        // ------------------------------------------------ adb
        private AdbManager GetAdb(string emuId)
        {
            if (!_adb.ContainsKey(emuId))
            {
                var emu = _detected.FirstOrDefault(e => e.Id == emuId);
                if (emu == null) return null;
                _adb[emuId] = new AdbManager(emu.InstallRoot);
            }
            return _adb[emuId];
        }

        private object AdbStatus(string emuId)
        {
            var mgr = GetAdb(emuId);
            if (mgr == null)
                return new { ok = false, message = "run detection first" };
            var st = mgr.Status();
            st["ok"] = st.ContainsKey("adb") && (bool)st["adb"];
            return st;
        }

        private object AdbConnect(string emuId, string port)
        {
            var mgr = GetAdb(emuId);
            if (mgr == null) return new { ok = false, message = "check adb status first" };
            var msg = mgr.Connect(port);
            Log("adb: " + msg);
            return new { ok = msg.ToLowerInvariant().Contains("connected"), message = msg };
        }

        private object AdbPackages(string emuId)
        {
            var mgr = GetAdb(emuId);
            if (mgr == null) return new { ok = false, packages = Array.Empty<object>() };
            var pkgs = mgr.ListPackages();
            return new
            {
                ok = true,
                packages = pkgs.Select(p => new
                {
                    name = p,
                    core = AdbManager.CoreSafe.Contains(p),
                    safe = mgr.MatchProfile(p, "safe"),
                    balanced = mgr.MatchProfile(p, "balanced"),
                    aggressive = mgr.MatchProfile(p, "aggressive"),
                }).ToList(),
            };
        }

        private object AdbUninstall(string emuId, List<string> packages)
        {
            var mgr = GetAdb(emuId);
            if (mgr == null) return new { ok = false, message = "not connected" };
            Log("adb uninstall: " + packages.Count + " package(s)");
            var results = mgr.Uninstall(packages);
            foreach (var kv in results) Log("  " + kv.Key + ": " + kv.Value);
            return new { ok = true, results, log = GetLog() };
        }

        // ------------------------------------------------ device mode (CoreUP-inspired)
        private DeviceManager GetDevice()
        {
            if (_device == null) _device = new DeviceManager();
            return _device;
        }

        private object DeviceStatus()
        {
            var mgr = GetDevice();
            var devs = mgr.Devices();
            bool hasAdb = !string.IsNullOrEmpty(mgr.AdbPath);
            if (mgr.Serial == "" && devs.Count > 0)
            {
                var first = devs.FirstOrDefault(d => (string)d["state"] == "device");
                if (first != null) mgr.Serial = (string)first["serial"];
            }
            string msg = !hasAdb
                ? "adb.exe not found — keep the platform-tools folder from the release zip next to EmuClean.exe"
                : mgr.Serial != "" ? "connected: " + mgr.Serial
                : devs.Count + " adb device(s) — pick one below";
            return new
            {
                adb = hasAdb,
                adb_path = mgr.AdbPath,
                devices = devs,
                serial = mgr.Serial,
                message = msg,
            };
        }

        private object DeviceConnect(string serial)
        {
            var mgr = GetDevice();
            if (string.IsNullOrEmpty(mgr.AdbPath))
                return new { ok = false, message = "adb.exe not found" };
            if (string.IsNullOrEmpty(serial))
            {
                var dev = mgr.Devices().FirstOrDefault(d => (string)d["state"] == "device");
                if (dev == null)
                    return new { ok = false, message = "no device in 'device' state — plug in the phone and allow USB debugging" };
                serial = (string)dev["serial"];
            }
            mgr.Connect(serial);
            Log("device: connected " + serial);
            return new { ok = true, message = "connected: " + serial };
        }

        private object DeviceReconnect()
        {
            var res = GetDevice().Reconnect();
            Log("device: reconnect — devices now: " +
                ((List<Dictionary<string, object>>)res["devices"]).Count);
            return res;
        }

        private object DeviceInfo()
        {
            var mgr = GetDevice();
            if (mgr.Serial == "") return new Dictionary<string, object> { ["ok"] = false, ["message"] = "no device connected" };
            var info = mgr.Info();
            info["ok"] = true;
            Log("device info: " + info["brand"] + " " + info["model"] + " — Android " + info["android"]);
            return info;
        }

        private object DevicePackages(bool thirdPartyOnly)
        {
            var mgr = GetDevice();
            if (mgr.Serial == "") return new { ok = false, packages = Array.Empty<object>() };
            var pkgs = mgr.Packages(thirdPartyOnly);
            return new { ok = true, packages = pkgs };
        }

        private object DeviceUninstall(List<string> packages)
        {
            var mgr = GetDevice();
            if (mgr.Serial == "") return new { ok = false, message = "no device connected" };
            Log("device uninstall: " + packages.Count + " package(s)");
            var results = mgr.Uninstall(packages);
            foreach (var kv in results) Log("  " + kv.Key + ": " + kv.Value);
            return new { ok = true, results, log = GetLog() };
        }

        private object DeviceRestore(string pkg)
        {
            var mgr = GetDevice();
            if (mgr.Serial == "") return new { ok = false, message = "no device connected" };
            var msg = mgr.Restore(pkg);
            Log("device restore " + pkg + ": " + msg);
            return new { ok = msg == "ok", message = msg == "ok" ? "restored " + pkg : msg };
        }

        private object DeviceForceStop()
        {
            var mgr = GetDevice();
            if (mgr.Serial == "") return new { ok = false, message = "no device connected" };
            var res = mgr.ForceStopAll();
            Log(string.Format("device force-stop ALL: {0} stopped, {1} failed", res["stopped"], res["failed"]));
            return new { ok = true, stopped = res["stopped"], failed = res["failed"], log = GetLog() };
        }

        private object DeviceDenyNotif()
        {
            var mgr = GetDevice();
            if (mgr.Serial == "") return new { ok = false, message = "no device connected" };
            var res = mgr.DenyNotificationsAll();
            if ((bool)res["sdk_ok"])
                Log(string.Format("device deny notifications ALL: {0} denied, {1} failed", res["denied"], res["failed"]));
            else
                Log("device deny notifications ALL: " + res["message"]);
            return new
            {
                ok = true,
                denied = res["denied"],
                failed = res["failed"],
                sdk_ok = res["sdk_ok"],
                message = res.ContainsKey("message") ? res["message"] : "",
                log = GetLog(),
            };
        }

        private object DeviceLs(string path)
        {
            var mgr = GetDevice();
            if (mgr.Serial == "") return new { ok = false, entries = Array.Empty<object>() };
            var entries = mgr.Ls(path);
            return new { ok = true, path, entries };
        }

        private static string PullDir()
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads", "EmuClean");
        }

        private object DevicePull(string remote, string localDir)
        {
            var mgr = GetDevice();
            if (mgr.Serial == "") return new { ok = false, message = "no device connected" };
            if (string.IsNullOrEmpty(localDir)) localDir = PullDir();
            var msg = mgr.Pull(remote, localDir);
            Log("device pull " + remote + " → " + localDir + " — " + msg);
            return new { ok = msg.StartsWith("pulled"), message = msg + " → " + localDir };
        }

        private object DevicePush(string local, string remote)
        {
            var mgr = GetDevice();
            if (mgr.Serial == "") return new { ok = false, message = "no device connected" };
            if (!System.IO.File.Exists(local))
                return new { ok = false, message = "local file not found: " + local };
            var msg = mgr.Push(local, remote);
            Log("device push " + local + " → " + remote + " — " + msg);
            return new { ok = msg.StartsWith("pushed"), message = msg };
        }

        private object DeviceQuickPull(string kind)
        {
            var mgr = GetDevice();
            if (mgr.Serial == "") return new { ok = false, message = "no device connected" };
            var src = mgr.QuickPullSource(kind);
            if (src == "") return new { ok = false, message = "no " + kind + " folder found on the device" };
            return DevicePull(src, null);
        }

        private object DeviceReboot(string mode)
        {
            var mgr = GetDevice();
            if (mgr.Serial == "") return new { ok = false, message = "no device connected" };
            var msg = mgr.Reboot(mode);
            Log("device reboot(" + mode + ")");
            return new { ok = true, message = msg };
        }

        // ------------------------------------------------ optimize
        private object OptimizerCatalog()
        {
            return new
            {
                android = Optimizer.AndroidTweaks.Select(t => new
                {
                    name = t.Name,
                    label = t.Label.Split('(')[0].Trim(),
                    note = t.Label.Contains("(") && t.Label.Contains(")")
                        ? t.Label.Substring(t.Label.IndexOf('(') + 1).Replace(")", "")
                        : ""
                }).ToList(),
                pc = new List<object>
                {
                    new { name = "power_high", label = "High Performance power plan",
                          note = "Switch the active Windows power plan from Balanced." },
                    new { name = "gamedvr_off", label = "Turn off Game Bar background recording",
                          note = "GameDVR capture steals CPU/GPU from emulators." },
                    new { name = "priority_high", label = "Raise emulator process priority",
                          note = "Set running emulator processes to High priority." },
                },
            };
        }

        private object ApplyOptimizer(List<string> androidNames, List<string> pcNames,
            string emuId)
        {
            var android = new Dictionary<string, string>();
            var pc = new Dictionary<string, object>();
            if (androidNames.Count > 0)
            {
                var mgr = GetAdb(emuId ?? "bluestacks5");
                if (mgr != null && !string.IsNullOrEmpty(mgr.Serial))
                {
                    var tweaks = Optimizer.AndroidTweaks
                        .Where(t => androidNames.Contains(t.Name))
                        .Select(t => new KeyValuePair<string, string>(t.Name, t.Args))
                        .ToList();
                    android = mgr.ApplySettings(tweaks);
                    foreach (var kv in android) Log("android tweak " + kv.Key + ": " + kv.Value);
                }
                else
                {
                    foreach (var n in androidNames) android[n] = "adb not connected";
                }
            }
            if (pcNames.Contains("power_high"))
            {
                pc["power_high"] = Optimizer.SetHighPerformancePower();
                Log("pc: high performance power plan");
            }
            if (pcNames.Contains("gamedvr_off"))
            {
                pc["gamedvr_off"] = Optimizer.GameDvrOff();
                Log("pc: GameDVR disabled");
            }
            if (pcNames.Contains("priority_high"))
            {
                pc["priority_high"] = Optimizer.SetEmulatorPriority();
                Log("pc: emulator process priority raised");
            }
            return new { android, pc, log = GetLog() };
        }
    }
}
