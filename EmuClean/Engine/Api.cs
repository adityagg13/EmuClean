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
            return new { emulators = outp, leftovers = Cleaner.FindLeftovers(), version = "1.0.0" };
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
