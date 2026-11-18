using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EmuClean.Engine
{
    /// <summary>
    /// Device Mode engine — works with any ADB device (physical phone over USB
    /// via bundled platform-tools, or an emulator instance over TCP).
    /// Inspired by EVGA CoreUP's Android Zen: BloatDroid (list / interactive
    /// uninstall / uninstall specific / Full Nuke), Files Transfer, Device
    /// Info, Reconnect, Reboot, Force Stop + notification deny for all apps.
    /// </summary>
    public class DeviceManager
    {
        public string AdbPath = "";
        public string Serial = "";

        public DeviceManager()
        {
            AdbPath = FindAdb();
        }

        // ------------------------------------------------ adb discovery
        private static string FindAdb()
        {
            // 1) adb bundled with EmuClean (platform-tools, release zip)
            var bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "platform-tools", "adb.exe");
            if (File.Exists(bundled)) return bundled;

            // 2) an emulator's own adb (BlueStacks HD-Adb etc.)
            var detected = Detector.Detect(true);
            foreach (var emu in detected.Where(e => e.Kind == "android" && e.Installed))
            {
                try
                {
                    foreach (var f in Directory.GetFiles(emu.InstallRoot, "*adb*.exe",
                        SearchOption.AllDirectories))
                        return f;
                }
                catch { }
            }
            // 3) adb already on PATH
            var psi = new ProcessStartInfo("cmd.exe", "/c where adb.exe")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            try
            {
                using (var p = Process.Start(psi))
                {
                    var line = p.StandardOutput.ReadLine();
                    if (!string.IsNullOrEmpty(line) && File.Exists(line.Trim())) return line.Trim();
                }
            }
            catch { }
            return "";
        }

        public static string Run(string exe, string args, int timeoutMs = 20000)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                };
                using (var p = Process.Start(psi))
                {
                    if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return "timeout"; }
                    return p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                }
            }
            catch (Exception e) { return "error: " + e.Message; }
        }

        private string Sh(string cmd, int timeoutMs = 20000)
        {
            if (string.IsNullOrEmpty(AdbPath) || string.IsNullOrEmpty(Serial)) return "";
            return Run(AdbPath, "-s " + Serial + " shell " + cmd, timeoutMs);
        }

        // ------------------------------------------------ devices
        public List<Dictionary<string, object>> Devices()
        {
            var list = new List<Dictionary<string, object>>();
            if (string.IsNullOrEmpty(AdbPath))
                return list;
            var outp = Run(AdbPath, "devices");
            foreach (var line in outp.Split('\n').Skip(1))
            {
                var parts = line.Trim().Split(new[] { ' ', '\t' },
                    StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && parts[0] != "List")
                    list.Add(new Dictionary<string, object>
                    {
                        ["serial"] = parts[0],
                        ["state"] = parts[1],
                        ["usb"] = parts[0].Contains(":") && parts[0].Contains("."),
                    });
            }
            return list;
        }

        public string Connect(string serial)
        {
            Serial = serial;
            return serial;
        }

        public Dictionary<string, object> Reconnect()
        {
            var res = new Dictionary<string, object>();
            if (string.IsNullOrEmpty(AdbPath))
            {
                res["ok"] = false;
                res["message"] = "adb.exe not found (bundle platform-tools or install an Android emulator)";
                return res;
            }
            Run(AdbPath, "kill-server", 8000);
            Run(AdbPath, "start-server", 8000);
            // re-attach known emulator instance ports
            var ports = new List<string>();
            foreach (var emu in Detector.Detect(true).Where(e => e.Kind == "android"))
            {
                var mgr = new AdbManager(emu.InstallRoot);
                ports.AddRange(AdbManager.ReadAdbPorts(mgr.Conf).Values);
            }
            ports.Add("5555"); // classic emulator port
            foreach (var port in ports.Distinct())
                Run(AdbPath, "connect 127.0.0.1:" + port, 6000);
            var devs = Devices();
            Serial = devs.FirstOrDefault(d => (string)d["state"] == "device") is Dictionary<string, object> first
                ? (string)first["serial"] : "";
            res["ok"] = devs.Count > 0;
            res["devices"] = devs;
            res["serial"] = Serial;
            return res;
        }

        // ------------------------------------------------ info
        public Dictionary<string, object> Info()
        {
            var info = new Dictionary<string, object>();
            var props = new Dictionary<string, string>();
            var dump = Sh("getprop", 15000);
            foreach (Match m in Regex.Matches(dump, @"\[([^\]]+)\]:\s*\[([^\]]*)\]"))
                props[m.Groups[1].Value] = m.Groups[2].Value;

            string Get(string k, string alt = "") =>
                props.ContainsKey(k) && props[k] != "" ? props[k] : alt;

            info["brand"] = Get("ro.product.brand", Get("ro.product.manufacturer", "?"));
            info["model"] = Get("ro.product.model", "?");
            info["device"] = Get("ro.product.device", "?");
            info["android"] = Get("ro.build.version.release", "?");
            info["sdk"] = Get("ro.build.version.sdk", "?");
            info["patch"] = Get("ro.build.version.security_patch", "?");
            info["build"] = Get("ro.build.display.id", "?");
            info["abi"] = Get("ro.product.cpu.abi", "?");
            info["serial"] = Serial;

            var size = Sh("wm size");
            var msize = Regex.Match(size, @"(\d+x\d+)");
            info["screen"] = msize.Success ? msize.Groups[1].Value : "?";
            info["density"] = Get("ro.sf.lcd_density", "?");

            var bat = Sh("dumpsys battery");
            info["battery_level"] = Regex.Match(bat, @"level:\s*(\d+)").Groups[1].Value;
            var bstat = Regex.Match(bat, @"status:\s*(\d+)").Groups[1].Value;
            info["battery_status"] = bstat == "2" ? "charging" : bstat == "3" ? "discharging"
                : bstat == "5" ? "full" : bstat == "" ? "?" : "idle";
            var btemp = Regex.Match(bat, @"temperature:\s*(\d+)").Groups[1].Value;
            info["battery_temp"] = btemp != "" ? (double.Parse(btemp) / 10).ToString("0.0") + " °C" : "?";

            var df = Sh("df /sdcard");
            foreach (var row in df.Split('\n').Skip(1).Reverse())
            {
                var c = row.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (c.Length >= 5 && long.TryParse(c[c.Length == 6 ? 1 : 0], out _))
                {
                    long blocks = long.Parse(c[c.Length == 6 ? 1 : 0]);
                    long avail = long.Parse(c[c.Length == 6 ? 3 : 2]);
                    info["storage_total"] = Human(blocks * 1024);
                    info["storage_free"] = Human(avail * 1024);
                    break;
                }
            }
            if (!info.ContainsKey("storage_total"))
            {
                info["storage_total"] = "?";
                info["storage_free"] = "?";
            }

            var mem = Sh("cat /proc/meminfo");
            var mmem = Regex.Match(mem, @"MemTotal:\s*(\d+)\s*kB");
            info["ram"] = mmem.Success ? Human(long.Parse(mmem.Groups[1].Value) * 1024) : "?";
            return info;
        }

        public static string Human(long n)
        {
            double d = n;
            if (n < 1024) return n + " B";
            if ((d /= 1024) < 1024) return d.ToString("F1") + " KB";
            if ((d /= 1024) < 1024) return d.ToString("F1") + " MB";
            d /= 1024;
            return d.ToString("F2") + " GB";
        }

        // ------------------------------------------------ BloatDroid
        /// <summary>Well-known global preinstalled bloat. All removals are
        /// `--user 0` (reversible) and previewed before Full Nuke runs.</summary>
        public static readonly string[] NukePatterns =
        {
            "^com\\.facebook\\..*", "^com\\.instagram\\..*", "^com\\.tiktok\\..*",
            "^com\\.zhiliaoapp\\..*", "^com\\.snapchat\\..*", "^com\\.twitter\\..*",
            "^com\\.reddit\\..*", "^com\\.linkedin\\..*", "^com\\.spotify\\..*",
            "^com\\.netflix\\..*", "^com\\.amazon\\.mShop.*", "^com\\.amazon\\.appmanager",
            "^com\\.amazon\\.avod.*", "^com\\.ebay\\..*", "^com\\.booking\\..*",
            "^com\\.microsoft\\.skydrive", "^com\\.microsoft\\.office.*",
            "^com\\.google\\.android\\.apps\\.tachyon", "^com\\.google\\.android\\.apps\\.docs",
            "^com\\.king\\..*", "^com\\.supercell\\..*", "^com\\.gameloft\\..*",
            "^com\\.zynga\\..*", "^com\\.flipkart\\.android$", "^com\\.hotstar$", "^in\\.mohalla\\..*",
        };

        public static bool MatchNuke(string pkg)
        {
            if (AdbManager.CoreSafe.Contains(pkg)) return false;
            return NukePatterns.Any(p => Regex.IsMatch(pkg, p, RegexOptions.IgnoreCase));
        }

        public List<Dictionary<string, object>> Packages(bool thirdPartyOnly = true)
        {
            var outp = new List<Dictionary<string, object>>();
            var third = Regex.Matches(Sh("pm list packages -3"), @"package:([\w.\-]+)")
                .Cast<Match>().Select(m => m.Groups[1].Value).ToHashSet();
            var sys = Regex.Matches(Sh("pm list packages -s"), @"package:([\w.\-]+)")
                .Cast<Match>().Select(m => m.Groups[1].Value).ToHashSet();
            foreach (var p in third.OrderBy(x => x))
                outp.Add(PkgRow(p, false, MatchNuke(p)));
            if (!thirdPartyOnly)
                foreach (var p in sys.OrderBy(x => x).Except(third))
                    outp.Add(PkgRow(p, true, MatchNuke(p)));
            return outp;
        }

        private static Dictionary<string, object> PkgRow(string name, bool system, bool nuke)
        {
            return new Dictionary<string, object>
            {
                ["name"] = name, ["system"] = system, ["nuke"] = nuke,
                ["core"] = AdbManager.CoreSafe.Contains(name),
            };
        }

        public Dictionary<string, string> Uninstall(List<string> packages)
        {
            var results = new Dictionary<string, string>();
            foreach (var pkg in packages)
            {
                if (AdbManager.CoreSafe.Contains(pkg))
                {
                    results[pkg] = "skipped (core safe-list)";
                    continue;
                }
                var outp = Sh("pm uninstall --user 0 " + pkg, 25000);
                results[pkg] = outp.Contains("Success") ? "ok" : outp.Trim();
                if (results[pkg].Length > 120) results[pkg] = results[pkg].Substring(0, 120);
            }
            return results;
        }

        public string Restore(string pkg)
        {
            var outp = Sh("cmd package install-existing " + pkg, 25000);
            return outp.Contains("Success") || outp.Contains("already") ? "ok" : outp.Trim();
        }

        // ------------------------------------------------ optimization
        public Dictionary<string, object> ForceStopAll()
        {
            var pkgs = Regex.Matches(Sh("pm list packages -3"), @"package:([\w.\-]+)")
                .Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            int stopped = 0, failed = 0;
            foreach (var p in pkgs)
            {
                var outp = Sh("am force-stop " + p, 8000);
                if (outp.Contains("Exception") || outp.Contains("error")) failed++;
                else stopped++;
            }
            return new Dictionary<string, object> { ["stopped"] = stopped, ["failed"] = failed };
        }

        public Dictionary<string, object> DenyNotificationsAll()
        {
            var sdk = Sh("getprop ro.build.version.sdk").Trim();
            var res = new Dictionary<string, object>();
            int sdkNum;
            if (!int.TryParse(sdk, out sdkNum) || sdkNum < 28)
            {
                res["denied"] = 0;
                res["failed"] = 0;
                res["sdk_ok"] = false;
                res["message"] = "needs Android 9+ (appops POST_NOTIFICATION) — this device reports SDK " +
                                 (sdk == "" ? "?" : sdk);
                return res;
            }
            var pkgs = Regex.Matches(Sh("pm list packages -3"), @"package:([\w.\-]+)")
                .Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            int denied = 0, failed = 0;
            foreach (var p in pkgs)
            {
                var outp = Sh("appops set " + p + " POST_NOTIFICATION deny", 8000);
                if (outp.Contains("Exception") || outp.Contains("not found")) failed++;
                else denied++;
            }
            res["denied"] = denied;
            res["failed"] = failed;
            res["sdk_ok"] = true;
            return res;
        }

        // ------------------------------------------------ Files Transfer
        public List<Dictionary<string, object>> Ls(string path)
        {
            var outp = Sh("ls -A -p \"" + path + "\"", 15000);
            var entries = new List<Dictionary<string, object>>();
            if (outp.Contains("No such file") || outp.Contains("error:") || outp.StartsWith("error"))
                return entries;
            foreach (var line in outp.Split('\n'))
            {
                var name = line.Trim();
                if (name == "") continue;
                bool isDir = name.EndsWith("/");
                entries.Add(new Dictionary<string, object>
                {
                    ["name"] = isDir ? name.TrimEnd('/') : name,
                    ["dir"] = isDir,
                });
            }
            return entries;
        }

        public string Pull(string remote, string localDir)
        {
            Directory.CreateDirectory(localDir);
            var outp = Run(AdbPath, "-s " + Serial + " pull \"" + remote + "\" \"" +
                Path.Combine(localDir, Path.GetFileName(remote.TrimEnd('/'))) + "\"", 120000);
            var m = Regex.Match(outp, @"(\d+) files? pulled");
            return m.Success ? "pulled " + m.Groups[1].Value + " file(s)" : outp.Trim();
        }

        public string Push(string local, string remote)
        {
            var outp = Run(AdbPath, "-s " + Serial + " push \"" + local + "\" \"" + remote + "\"", 120000);
            var m = Regex.Match(outp, @"(\d+) files? pushed");
            return m.Success ? "pushed " + m.Groups[1].Value + " file(s)" : outp.Trim();
        }

        public string QuickPullSource(string kind)
        {
            var candidates = kind == "screenshots"
                ? new[] { "/sdcard/Pictures/Screenshots", "/sdcard/Screenshots", "/sdcard/ScreenShots" }
                : kind == "camera" ? new[] { "/sdcard/DCIM/Camera" }
                : new[] { "/sdcard/Download" };
            foreach (var c in candidates)
            {
                var t = Sh("test -d \"" + c + "\" && echo y", 6000);
                if (t.Contains("y")) return c;
            }
            return "";
        }

        public string Reboot(string mode)
        {
            return mode == "system" || string.IsNullOrEmpty(mode)
                ? Sh("reboot", 8000) == "" ? "rebooting…" : Sh("reboot", 8000)
                : Run(AdbPath, "-s " + Serial + " reboot " + mode, 10000).Trim() == ""
                    ? "rebooting to " + mode + "…"
                    : "reboot " + mode + " sent";
        }
    }
}
