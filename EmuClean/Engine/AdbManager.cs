using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EmuClean.Engine
{
    /// <summary>
    /// BlueStacks deep clean over ADB: HD-Adb.exe discovery, instance adb ports
    /// from bluestacks.conf, package listing, profile-based junk matching with a
    /// hard core safe-list, `pm uninstall --user 0` and `settings put global`.
    /// </summary>
    public class AdbManager
    {
        public const string AdbName = "HD-Adb.exe";

        private static readonly string[] ConfCandidates =
        {
            @"%ProgramData%\BlueStacks_nxt\bluestacks.conf",
            @"%ProgramData%\BlueStacks\bluestacks.conf",
        };

        // never removable — device boot + basics
        public static readonly HashSet<string> CoreSafe = new HashSet<string>
        {
            "com.android.phone", "com.android.settings", "com.android.systemui",
            "com.android.camera2", "com.android.contacts", "com.android.keychain",
            "com.android.gallery3d", "com.android.providers.contacts",
            "com.android.providers.telephony", "com.android.providers.media",
            "com.android.providers.downloads", "com.android.providers.settings",
            "com.android.location.fused", "com.android.server.telecom",
            "com.android.externalstorage", "com.android.documentsui",
            "com.google.android.gms", "com.google.android.gsf",
            "com.google.android.play.games", "com.google.android.gsf.login",
            "com.android.launcher3", "com.android.vending",
        };

        public static readonly Dictionary<string, string[]> Profiles =
            new Dictionary<string, string[]>
        {
            ["safe"] = new[]
            {
                @"^com\.bluestacks\.sapp", @"^com\.bluestacks\.gamepublishtool",
                @"^com\.bluestacks\.tport", @"^com\.bluestacks\.bstrpc",
                @"^com\.bluestacks\.fitness", @"^com\.bluestacks\.tour",
            },
            ["balanced"] = new[]
            {
                @"^com\.bluestacks\.(?!settings|appprovider|service|frontend|launcher)",
                @"^com\.google\.android\.apps\.tachyon$",
                @"^com\.google\.android\.youtube\.tv",
                @"^com\.google\.android\.videos",
                @"^com\.google\.android\.music",
            },
            ["aggressive"] = new[]
            {
                @"^com\.king\.com\.", @"^com\.king\.candycrush",
                @"^com\.supercell\.", @"^com\.miniclip\.",
                @"^com\.zynga\.", @"^com\.outfit7\.",
                @"^com\.facebook\.", @"^com\.instagram\.",
                @"^com\.twitter\.android", @"^com\.snapchat\.android",
                @"^com\.spotify\.music",
            },
        };

        public string AdbPath = "";
        public string Serial = "";
        public string Conf = "";

        public AdbManager(string installRoot)
        {
            AdbPath = FindAdb(installRoot ?? "");
            foreach (var c in ConfCandidates)
            {
                var p = Environment.ExpandEnvironmentVariables(c);
                if (File.Exists(p)) { Conf = p; break; }
            }
        }

        private static string FindAdb(string installRoot)
        {
            var cands = new List<string> { Path.Combine(installRoot, AdbName) };
            try
            {
                foreach (var f in Directory.GetFiles(installRoot, AdbName, SearchOption.AllDirectories))
                    cands.Add(f);
            }
            catch { }
            return cands.FirstOrDefault(File.Exists) ?? "";
        }

        public static string Run(string exe, string args, int timeoutMs = 15000)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (var p = Process.Start(psi))
                {
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        return "timeout";
                    }
                    return p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public static Dictionary<string, string> ReadAdbPorts(string confPath)
        {
            var ports = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(confPath) || !File.Exists(confPath)) return ports;
            try
            {
                var text = File.ReadAllText(confPath);
                var rx = new Regex(
                    @"bst\.instance\.([A-Za-z0-9_\-]+)\.status\.adb_port=""(\d+)""");
                foreach (Match m in rx.Matches(text))
                {
                    var port = m.Groups[2].Value;
                    if (port != "0" && port != "")
                        ports[m.Groups[1].Value] = port;
                }
            }
            catch { }
            return ports;
        }

        public Dictionary<string, object> Status()
        {
            if (string.IsNullOrEmpty(AdbPath))
                return new Dictionary<string, object>
                {
                    ["adb"] = false,
                    ["message"] = "HD-Adb.exe not found in the BlueStacks folder"
                };
            var ports = ReadAdbPorts(Conf);
            return new Dictionary<string, object>
            {
                ["adb"] = true,
                ["adb_path"] = AdbPath,
                ["conf"] = Conf,
                ["ports"] = ports,
                ["message"] = ports.Count + " instance port(s) found",
            };
        }

        public string Connect(string port = null)
        {
            if (string.IsNullOrEmpty(port))
            {
                var ports = ReadAdbPorts(Conf);
                port = ports.Values.FirstOrDefault();
            }
            if (string.IsNullOrEmpty(port))
                return "no adb port available — is BlueStacks running?";
            var outp = Run(AdbPath, "connect 127.0.0.1:" + port);
            var m = Regex.Match(outp, @"(connected to|already connected)[^\r\n]*", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                Serial = "127.0.0.1:" + port;
                return "connected: " + Serial;
            }
            return outp.Trim();
        }

        private string Shell(string cmd)
        {
            if (string.IsNullOrEmpty(Serial)) return "";
            return Run(AdbPath, "-s " + Serial + " shell " + cmd, 25000);
        }

        public List<string> ListPackages()
        {
            var outp = Shell("pm list packages -3");
            return Regex.Matches(outp, @"package:([\w.\-]+)")
                .Cast<Match>().Select(m => m.Groups[1].Value)
                .OrderBy(x => x).ToList();
        }

        public bool MatchProfile(string package, string profile)
        {
            if (CoreSafe.Contains(package)) return false;
            string[] patterns;
            if (!Profiles.TryGetValue(profile, out patterns)) return false;
            return patterns.Any(p => Regex.IsMatch(package, p, RegexOptions.IgnoreCase));
        }

        public Dictionary<string, string> Uninstall(List<string> packages)
        {
            var results = new Dictionary<string, string>();
            foreach (var pkg in packages)
            {
                if (CoreSafe.Contains(pkg))
                {
                    results[pkg] = "skipped (core safe-list)";
                    continue;
                }
                var outp = Shell("pm uninstall --user 0 " + pkg);
                results[pkg] = outp.Contains("Success") ? "ok" : outp.Trim();
                if (results[pkg].Length > 120) results[pkg] = results[pkg].Substring(0, 120);
            }
            return results;
        }

        public Dictionary<string, string> ApplySettings(
            List<KeyValuePair<string, string>> tweaks)
        {
            var results = new Dictionary<string, string>();
            foreach (var t in tweaks)
            {
                var outp = Shell("settings put global " + t.Value);
                var low = outp.ToLowerInvariant();
                results[t.Key] = (outp.Contains("Exception") || low.Contains("failed"))
                    ? outp.Trim() : "ok";
            }
            return results;
        }
    }
}
