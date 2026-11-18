using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;

namespace EmuClean.Engine
{
    public class Tweak
    {
        public string Name, Label, Args;
        public Tweak(string name, string label, string args)
        {
            Name = name; Label = label; Args = args;
        }
    }

    public static class Optimizer
    {
        public static readonly List<Tweak> AndroidTweaks = new List<Tweak>
        {
            new Tweak("cached_apps_freezer", "Enable cached apps freezer (frees RAM in background apps)", "cached_apps_freezer enabled"),
            new Tweak("activity_logging", "Disable activity starts logging (less overhead)", "activity_starts_logging_enabled 0"),
            new Tweak("package_verifier", "Disable package verifier (less install lag)", "verifier_verify_adb_installs 0"),
            new Tweak("show_processes", "Hide background process count (cleaner, less work)", "show_processes 0"),
            new Tweak("dirty_regions", "Disable dirty-region debug drawing", "show_dirty_regions 0"),
            new Tweak("wifi_display", "Disable Wi-Fi display service", "wifi_display_on 0"),
            new Tweak("fstrim", "Make fstrim more aggressive (keeps storage snappy)", "fstrim_mandatory_interval 600"),
        };

        public static readonly string[] ProcessTargets =
        {
            "HD-Player", "BlueStacks", "dnplayer", "ld9box",
            "Nox", "MEmu", "MuMuPlayer", "LdBoxHeadless",
            "MEmuHeadless", "NoxVMHandle", "MuMuVMMHeadless",
            "Dolphin", "pcsx2", "pcsx2-qt", "rpcs3", "yuzu",
            "suyu", "Ryujinx", "Ryujinx.Ava", "PPSSPPWindows64",
            "Cemu", "xenia", "duckstation-qt-x64", "retroarch",
        };

        private static string Run(string exe, string args, int timeoutMs = 20000)
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
                    if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return "timeout"; }
                    return p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                }
            }
            catch (Exception e) { return e.Message; }
        }

        public static string SetHighPerformancePower()
        {
            var outp = Run("powercfg", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
            return outp.ToLowerInvariant().Contains("not found") ? outp : "ok";
        }

        public static string RestoreBalancedPower()
        {
            var outp = Run("powercfg", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e");
            return outp.ToLowerInvariant().Contains("not found") ? outp : "ok";
        }

        public static Dictionary<string, string> GameDvrOff()
        {
            var res = new Dictionary<string, string>();
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(@"System\GameConfigStore"))
                    k.SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord);
                res["gameconfigstore"] = "ok";
            }
            catch (Exception e) { res["gameconfigstore"] = e.Message; }
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\GameDVR"))
                    k.SetValue("AppCaptureEnabled", 0, RegistryValueKind.DWord);
                res["gamedvr_user"] = "ok";
            }
            catch (Exception e) { res["gamedvr_user"] = e.Message; }
            try
            {
                using (var k = Registry.LocalMachine.CreateSubKey(
                    @"SOFTWARE\Policies\Microsoft\Windows\GameDVR"))
                    k.SetValue("AllowGameDVR", 0, RegistryValueKind.DWord);
                res["gamedvr_policy"] = "ok";
            }
            catch (Exception e) { res["gamedvr_policy"] = e.Message; }
            return res;
        }

        public static Dictionary<string, object> SetEmulatorPriority()
        {
            var raised = new List<string>();
            var notRunning = new List<string>();
            foreach (var name in ProcessTargets)
            {
                var procs = Process.GetProcessesByName(name);
                if (procs.Length == 0) { notRunning.Add(name); continue; }
                foreach (var p in procs)
                {
                    try { p.PriorityClass = ProcessPriorityClass.High; raised.Add(name); }
                    catch { }
                }
            }
            return new Dictionary<string, object>
            {
                ["raised"] = raised.Distinct().ToList(),
                ["not_running"] = notRunning,
            };
        }

        public static List<string> RunningTargets()
        {
            var running = new List<string>();
            var procs = Process.GetProcesses();
            var names = new HashSet<string>(procs.Select(p => p.ProcessName), StringComparer.OrdinalIgnoreCase);
            foreach (var t in ProcessTargets)
                if (names.Contains(t)) running.Add(t + ".exe");
            return running;
        }
    }
}
