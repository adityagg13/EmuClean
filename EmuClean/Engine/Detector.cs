using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace EmuClean.Engine
{
    public class EmuDef
    {
        public string Id, Name, Kind;
        public string[] UninstallKeywords, Processes, InstallPaths, DataPaths;
    }

    public class DetectedEmu
    {
        public string Id, Name, Kind;
        public bool Installed, Running;
        public string InstallRoot = "", DataRoot = "";
    }

    public class Rule
    {
        public string Category, Label, Note;
        public string[] Templates;
        public Rule(string cat, string label, string[] templates, string note)
        {
            Category = cat; Label = label; Templates = templates; Note = note;
        }
    }

    public static class Detector
    {
        private const string PF = "%ProgramFiles%";
        private const string PF86 = "%ProgramFiles(x86)%";
        private const string LOCAL = "%LocalAppData%";
        private const string AD = "%AppData%";
        private const string PD = "%ProgramData%";
        private const string USER = "%UserProfile%";
        private const string DOCS = "%MyDocs%";
        private const string DOCS_ALT = "%MyDocsAlt%";

        private static string Expand(string p)
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var docsAlt = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "OneDrive", "Documents");
            return Environment.ExpandEnvironmentVariables(
                p.Replace("%MyDocsAlt%", docsAlt).Replace("%MyDocs%", docs));
        }

        public static string ExpandPath(string p) { return Expand(p); }

        // ------------------------------------------------- emulator registry
        public static readonly List<EmuDef> Emulators = new List<EmuDef>
        {
            new EmuDef { Id="bluestacks5", Name="BlueStacks 5", Kind="android",
                UninstallKeywords=new[]{"bluestacks"},
                Processes=new[]{"HD-Player.exe","BlueStacks.exe","BstkSVC.exe","HD-Adb.exe","HD-Agent.exe","HD-RunApp.exe","HD-Frontend.exe","Frontend.exe","BlueStacksHelper.exe"},
                InstallPaths=new[]{PF+@"\BlueStacks_nxt", PF86+@"\BlueStacks_nxt", PF+@"\BlueStacks_msi5", PF+@"\BlueStacks", PF86+@"\BlueStacks"},
                DataPaths=new[]{PD+@"\BlueStacks_nxt", PD+@"\BlueStacks", LOCAL+@"\BlueStacks_nxt", LOCAL+@"\BlueStacks"} },
            new EmuDef { Id="bluestacks4", Name="BlueStacks 4", Kind="android",
                UninstallKeywords=new[]{"bluestacks"},
                Processes=new[]{"BlueStacks.exe","BstkSVC.exe","HD-Adb.exe"},
                InstallPaths=new[]{PF86+@"\BlueStacks", PF+@"\BlueStacks"},
                DataPaths=new[]{PD+@"\BlueStacks", LOCAL+@"\BlueStacks"} },
            new EmuDef { Id="ldplayer", Name="LDPlayer 9", Kind="android",
                UninstallKeywords=new[]{"ldplayer"},
                Processes=new[]{"dnplayer.exe","LdBoxHeadless.exe","LdVBoxSVC.exe","ld9box.exe","Ld9BoxHeadless.exe","LdVBoxHeadless.exe"},
                InstallPaths=new[]{@"C:\LDPlayer\LDPlayer9", @"C:\LDPlayer\LDPlayer4", PF+@"\LDPlayer\LDPlayer9", PF86+@"\LDPlayer\LDPlayer9"},
                DataPaths=new[]{@"C:\LDPlayer\LDPlayer9", LOCAL+@"\LDPlayer"} },
            new EmuDef { Id="nox", Name="NoxPlayer", Kind="android",
                UninstallKeywords=new[]{"nox"},
                Processes=new[]{"Nox.exe","NoxVMHandle.exe","NoxService.exe","nox_adb.exe"},
                InstallPaths=new[]{PF86+@"\Nox", PF86+@"\Nox_bin", PF+@"\Nox"},
                DataPaths=new[]{LOCAL+@"\Nox", AD+@"\Nox"} },
            new EmuDef { Id="memu", Name="MEmu Play", Kind="android",
                UninstallKeywords=new[]{"memu","microvirt"},
                Processes=new[]{"MEmu.exe","MEmuHeadless.exe","MemuService.exe"},
                InstallPaths=new[]{PF+@"\Microvirt\MEmu", PF86+@"\Microvirt\MEmu"},
                DataPaths=new[]{LOCAL+@"\Microvirt", AD+@"\Microvirt"} },
            new EmuDef { Id="gameloop", Name="Gameloop", Kind="android",
                UninstallKeywords=new[]{"gameloop","tencent"},
                Processes=new[]{"androidemulator.exe","aow_exe.exe","GameLoop.exe","TGP.exe"},
                InstallPaths=new[]{PF86+@"\Gameloop", PF+@"\Gameloop", LOCAL+@"\GameLoop"},
                DataPaths=new[]{LOCAL+@"\Tencent\GameLoop", AD+@"\Tencent\GameLoop"} },
            new EmuDef { Id="mumu", Name="MuMu Player", Kind="android",
                UninstallKeywords=new[]{"mumu","netease"},
                Processes=new[]{"MuMuPlayer.exe","MuMuVMMHeadless.exe","NemuHeadless.exe","MuMuNxDevice.exe","MuMuPlayerGlobal.exe"},
                InstallPaths=new[]{PF+@"\Netease\MuMuPlayer-12.0", PF+@"\Netease\MuMu", PF86+@"\Netease\MuMu", PF+@"\MuMu"},
                DataPaths=new[]{LOCAL+@"\Netease\MuMu", AD+@"\Netease"} },
            new EmuDef { Id="koplayer", Name="KOPlayer", Kind="android",
                UninstallKeywords=new[]{"koplayer"},
                Processes=new[]{"KOPlayer.exe","KVMHeadless.exe"},
                InstallPaths=new[]{PF86+@"\KOPLAYER", PF+@"\KOPLAYER"},
                DataPaths=new[]{LOCAL+@"\KOPLAYER"} },
            new EmuDef { Id="genymotion", Name="Genymotion", Kind="android",
                UninstallKeywords=new[]{"genymotion"},
                Processes=new[]{"genymotion.exe","GenymotionPlayer.exe"},
                InstallPaths=new[]{PF+@"\Genymobile\Genymotion"},
                DataPaths=new[]{LOCAL+@"\Genymobile", AD+@"\Genymobile"} },
            new EmuDef { Id="dolphin", Name="Dolphin (Wii/GameCube)", Kind="desktop",
                UninstallKeywords=new[]{"dolphin"},
                Processes=new[]{"Dolphin.exe","DolphinQt2.exe","DolphinWX.exe"},
                InstallPaths=new[]{PF+@"\Dolphin", PF86+@"\Dolphin", LOCAL+@"\Dolphin"},
                DataPaths=new[]{DOCS+@"\Dolphin Emulator", DOCS_ALT+@"\Dolphin Emulator", LOCAL+@"\Dolphin Emulator", AD+@"\Dolphin Emulator"} },
            new EmuDef { Id="pcsx2", Name="PCSX2 (PS2)", Kind="desktop",
                UninstallKeywords=new[]{"pcsx2"},
                Processes=new[]{"pcsx2.exe","pcsx2-qt.exe"},
                InstallPaths=new[]{PF+@"\PCSX2", PF86+@"\PCSX2", LOCAL+@"\PCSX2"},
                DataPaths=new[]{DOCS+@"\PCSX2", DOCS_ALT+@"\PCSX2", AD+@"\PCSX2", LOCAL+@"\PCSX2"} },
            new EmuDef { Id="rpcs3", Name="RPCS3 (PS3)", Kind="desktop",
                UninstallKeywords=new[]{"rpcs3"},
                Processes=new[]{"rpcs3.exe"},
                InstallPaths=new[]{PF+@"\RPCS3", LOCAL+@"\RPCS3"},
                DataPaths=new[]{LOCAL+@"\RPCS3", AD+@"\RPCS3"} },
            new EmuDef { Id="yuzu", Name="Yuzu (Switch)", Kind="desktop",
                UninstallKeywords=new[]{"yuzu"},
                Processes=new[]{"yuzu.exe"},
                InstallPaths=new[]{PF+@"\yuzu", LOCAL+@"\yuzu"},
                DataPaths=new[]{AD+@"\yuzu", LOCAL+@"\yuzu"} },
            new EmuDef { Id="suyu", Name="Suyu (Switch)", Kind="desktop",
                UninstallKeywords=new[]{"suyu"},
                Processes=new[]{"suyu.exe"},
                InstallPaths=new[]{LOCAL+@"\suyu"},
                DataPaths=new[]{AD+@"\suyu", LOCAL+@"\suyu"} },
            new EmuDef { Id="ryujinx", Name="Ryujinx (Switch)", Kind="desktop",
                UninstallKeywords=new[]{"ryujinx"},
                Processes=new[]{"Ryujinx.exe","Ryujinx.Ava.exe"},
                InstallPaths=new[]{PF+@"\Ryujinx", LOCAL+@"\Ryujinx"},
                DataPaths=new[]{AD+@"\Ryujinx", LOCAL+@"\Ryujinx"} },
            new EmuDef { Id="citralime", Name="Lime3DS / Citra (3DS)", Kind="desktop",
                UninstallKeywords=new[]{"citra","lime3ds"},
                Processes=new[]{"citra-qt.exe","lime-qt.exe","citra.exe"},
                InstallPaths=new[]{PF+@"\Lime3DS", LOCAL+@"\Citra", LOCAL+@"\Lime3DS"},
                DataPaths=new[]{AD+@"\Citra", AD+@"\Lime3DS", LOCAL+@"\Citra", LOCAL+@"\Lime3DS"} },
            new EmuDef { Id="ppsspp", Name="PPSSPP (PSP)", Kind="desktop",
                UninstallKeywords=new[]{"ppsspp"},
                Processes=new[]{"PPSSPP.exe","PPSSPPWindows64.exe"},
                InstallPaths=new[]{PF+@"\PPSSPP", LOCAL+@"\PPSSPP"},
                DataPaths=new[]{AD+@"\PPSSPP", LOCAL+@"\PPSSPP"} },
            new EmuDef { Id="cemu", Name="Cemu (Wii U)", Kind="desktop",
                UninstallKeywords=new[]{"cemu"},
                Processes=new[]{"Cemu.exe"},
                InstallPaths=new[]{PF+@"\Cemu", LOCAL+@"\Cemu"},
                DataPaths=new[]{LOCAL+@"\Cemu", AD+@"\Cemu"} },
            new EmuDef { Id="xenia", Name="Xenia (Xbox 360)", Kind="desktop",
                UninstallKeywords=new[]{"xenia"},
                Processes=new[]{"xenia.exe","xenia_canary.exe"},
                InstallPaths=new[]{PF+@"\Xenia", LOCAL+@"\Xenia"},
                DataPaths=new[]{LOCAL+@"\Xenia", AD+@"\Xenia"} },
            new EmuDef { Id="duckstation", Name="DuckStation (PS1)", Kind="desktop",
                UninstallKeywords=new[]{"duckstation"},
                Processes=new[]{"duckstation-qt-x64.exe","duckstation-qt.exe"},
                InstallPaths=new[]{PF+@"\DuckStation", LOCAL+@"\DuckStation"},
                DataPaths=new[]{DOCS+@"\DuckStation", DOCS_ALT+@"\DuckStation", AD+@"\DuckStation", LOCAL+@"\DuckStation"} },
            new EmuDef { Id="retroarch", Name="RetroArch", Kind="desktop",
                UninstallKeywords=new[]{"retroarch"},
                Processes=new[]{"retroarch.exe"},
                InstallPaths=new[]{PF+@"\RetroArch-Win64", PF86+@"\RetroArch", PF+@"\RetroArch", LOCAL+@"\RetroArch"},
                DataPaths=new[]{AD+@"\RetroArch", LOCAL+@"\RetroArch"} },
            new EmuDef { Id="mgba", Name="mGBA (GBA)", Kind="desktop",
                UninstallKeywords=new[]{"mgba"},
                Processes=new[]{"mgba-qt.exe","mgba-sdl.exe"},
                InstallPaths=new[]{PF+@"\mGBA", PF86+@"\mGBA"},
                DataPaths=new[]{AD+@"\mgba", DOCS+@"\mGBA"} },
            new EmuDef { Id="project64", Name="Project64 (N64)", Kind="desktop",
                UninstallKeywords=new[]{"project64"},
                Processes=new[]{"Project64.exe"},
                InstallPaths=new[]{PF86+@"\Project64 3.0", PF+@"\Project64"},
                DataPaths=new[]{AD+@"\Project64", LOCAL+@"\Project64"} },
            new EmuDef { Id="vita3k", Name="Vita3K (PS Vita)", Kind="desktop",
                UninstallKeywords=new[]{"vita3k"},
                Processes=new[]{"Vita3K.exe"},
                InstallPaths=new[]{PF+@"\Vita3K", LOCAL+@"\Vita3K"},
                DataPaths=new[]{AD+@"\Vita3K", LOCAL+@"\Vita3K"} },
            new EmuDef { Id="xemu", Name="xemu (Original Xbox)", Kind="desktop",
                UninstallKeywords=new[]{"xemu"},
                Processes=new[]{"xemu.exe"},
                InstallPaths=new[]{PF+@"\xemu", LOCAL+@"\xemu"},
                DataPaths=new[]{AD+@"\xemu", LOCAL+@"\xemu"} },
        };

        // ------------------------------------------------- cleanup rules
        public static readonly Dictionary<string, List<Rule>> Rules = new Dictionary<string, List<Rule>>
        {
            ["*"] = new List<Rule>
            {
                new Rule("junk", "Log files", new[]{"logs","log","Log"}, "*.log files everywhere"),
                new Rule("junk", "Crash dumps", new[]{"crashdumps","crash_dumps","CrashDumps","dumps","Dump"}, "*.dmp crash reports"),
            },
            ["bluestacks5"] = new List<Rule>
            {
                new Rule("junk", "Service & engine logs", new[]{"%DATA%\\Logs","%DATA%\\Engine\\Logs"}, "BlueStacks service logs"),
                new Rule("junk", "Crash dumps", new[]{"%DATA%\\CrashDumps","%DATA%\\Engine\\*.dmp"}, "crash reports"),
                new Rule("junk", "Installer cache", new[]{"%DATA%\\*.tmp"}, "leftover installer temp"),
                new Rule("opt", "Shared folder", new[]{"%DATA%\\BstkSharedFolder"}, "files you moved in/out of BlueStacks"),
                new Rule("lock", "Instances (apps & data)", new[]{"%DATA%\\Engine"}, "your Android instances — games, apps, accounts"),
                new Rule("lock", "Player configs", new[]{"%DATA%\\Config"}, "controls, resolution, preferences"),
            },
            ["bluestacks4"] = new List<Rule>
            {
                new Rule("junk", "Logs", new[]{"%DATA%\\Logs","%DATA%\\Engine\\Logs"}, "service logs"),
                new Rule("junk", "Crash dumps", new[]{"%DATA%\\CrashDumps"}, "crash reports"),
                new Rule("lock", "Instances (apps & data)", new[]{"%DATA%\\Engine"}, "your Android instances"),
            },
            ["ldplayer"] = new List<Rule>
            {
                new Rule("junk", "Logs", new[]{"logs","%ROOT%\\logs"}, "LDPlayer logs"),
                new Rule("junk", "Crash dumps", new[]{"*.dmp"}, "crash reports"),
                new Rule("lock", "VMs (apps & data)", new[]{"vms"}, "your Android VM images"),
            },
            ["nox"] = new List<Rule>
            {
                new Rule("junk", "Logs", new[]{"%ROOT%\\log","log"}, "Nox logs"),
                new Rule("junk", "Crash dumps", new[]{"*.dmp"}, "crash reports"),
                new Rule("lock", "VM images (apps & data)", new[]{"%ROOT%\\Images","Images","vms"}, "your Android instance"),
                new Rule("lock", "Configs", new[]{"%ROOT%\\bin\\BignoxVMS"}, "vm configuration"),
            },
            ["memu"] = new List<Rule>
            {
                new Rule("junk", "Logs", new[]{"%ROOT%\\log","log"}, "MEmu logs"),
                new Rule("lock", "VM images (apps & data)", new[]{"vms","%DATA%\\vms"}, "your Android instance"),
            },
            ["gameloop"] = new List<Rule>
            {
                new Rule("junk", "Logs & crash", new[]{"%DATA%\\log","log","crashdumps"}, "Gameloop/Tencent logs"),
                new Rule("junk", "Package cache", new[]{"%DATA%\\packages"}, "downloaded update packages"),
                new Rule("lock", "Game data", new[]{"%DATA%\\AndroidTFSS","AndroidTP"}, "installed game data"),
            },
            ["mumu"] = new List<Rule>
            {
                new Rule("junk", "Logs", new[]{"%DATA%\\logs","log"}, "MuMu logs"),
                new Rule("lock", "VM images (apps & data)", new[]{"%DATA%\\vms","vms"}, "your Android instance"),
            },
            ["koplayer"] = new List<Rule>
            {
                new Rule("junk", "Logs", new[]{"log"}, "KOPlayer logs"),
                new Rule("lock", "VM images", new[]{"vms"}, "your Android instance"),
            },
            ["genymotion"] = new List<Rule>
            {
                new Rule("junk", "Logs", new[]{"%DATA%\\logs","log"}, "Genymotion logs"),
                new Rule("lock", "Virtual devices", new[]{"%DATA%\\Genymobile\\Genymotion\\deployed"}, "your devices"),
            },
            ["dolphin"] = new List<Rule>
            {
                new Rule("junk", "Cache", new[]{"Cache","%DATA%\\Cache"}, "shader/material cache — regenerates"),
                new Rule("junk", "Dumps", new[]{"Dump","%DATA%\\Dump"}, "debug dumps"),
                new Rule("junk", "Logs", new[]{"%DATA%\\Logs","Logs"}, "emulator logs"),
                new Rule("opt", "Screenshots & recordings", new[]{"%DATA%\\ScreenShots","%DATA%\\Movies"}, "your captures — check first"),
                new Rule("lock", "GameCube saves", new[]{"%DATA%\\GC"}, "memory card saves"),
                new Rule("lock", "Wii saves & NAND", new[]{"%DATA%\\Wii"}, "Wii saves, Mii data, NAND"),
                new Rule("lock", "Save states", new[]{"%DATA%\\StateSaves"}, "your save states"),
                new Rule("lock", "Configs", new[]{"%DATA%\\Config","Config"}, "settings, controllers, games list"),
                new Rule("lock", "Game Mods (Riivolution)", new[]{"%DATA%\\Load"}, "mods and texture packs"),
            },
            ["pcsx2"] = new List<Rule>
            {
                new Rule("junk", "Cache", new[]{"%DATA%\\cache","cache"}, "shader/gamefix cache"),
                new Rule("junk", "Logs & dumps", new[]{"%DATA%\\logs","%DATA%\\dumps","logs","dumps"}, "debug output"),
                new Rule("junk", "Snapshots (shader)", new[]{"%DATA%\\snaps"}, "internal snapshots"),
                new Rule("opt", "Screenshots", new[]{"snaps","%DATA%\\snaps"}, "your screenshots — check first"),
                new Rule("lock", "Memory cards", new[]{"memcards","%DATA%\\memcards"}, "PS2 virtual memory cards"),
                new Rule("lock", "Save states", new[]{"sstates","%DATA%\\sstates"}, "your save states"),
                new Rule("lock", "BIOS", new[]{"bios","%DATA%\\bios"}, "required to run games"),
                new Rule("lock", "Configs", new[]{"inis","%DATA%\\inis"}, "per-game and global settings"),
            },
            ["rpcs3"] = new List<Rule>
            {
                new Rule("junk", "Logs", new[]{"%DATA%\\Logs","GUI\\*.log"}, "RPCS3 logs"),
                new Rule("junk", "Shader cache", new[]{"%DATA%\\cache","cache"}, "PPU/SPU & GPU caches — regenerates"),
                new Rule("junk", "Crash dumps", new[]{"%DATA%\\crash_dumps"}, "crash reports"),
                new Rule("lock", "Games & virtual HDD", new[]{"dev_hdd0","dev_hdd1","games"}, "installed games, saves, trophies"),
                new Rule("lock", "Configs", new[]{"config","GuiConfigs"}, "settings and per-game configs"),
            },
            ["yuzu"] = new List<Rule>
            {
                new Rule("junk", "Shader cache", new[]{"%LOCAL%\\yuzu\\cache","cache"}, "game shader caches — regenerates"),
                new Rule("junk", "Logs & telemetry", new[]{"%LOCAL%\\yuzu\\log","log"}, "debug logs"),
                new Rule("junk", "Crash dumps", new[]{"%LOCAL%\\yuzu\\crash_dumps"}, "crash reports"),
                new Rule("lock", "Games list & NAND", new[]{"%LOCAL%\\yuzu\\nand","nand"}, "saves, system data"),
                new Rule("lock", "SD card contents", new[]{"%LOCAL%\\yuzu\\sdmc","sdmc"}, "SD card saves/DLC"),
                new Rule("lock", "Mods & updates", new[]{"%LOCAL%\\yuzu\\load","load"}, "mods and update layers"),
                new Rule("lock", "Configs", new[]{"config","%AppData%\\yuzu\\config"}, "settings, game profiles"),
            },
            ["suyu"] = new List<Rule>
            {
                new Rule("junk", "Shader cache", new[]{"%LocalAppData%\\suyu\\cache","cache"}, "shader caches"),
                new Rule("junk", "Logs", new[]{"%LocalAppData%\\suyu\\log","log"}, "debug logs"),
                new Rule("lock", "NAND & SD", new[]{"%LocalAppData%\\suyu\\nand","%LocalAppData%\\suyu\\sdmc"}, "saves and data"),
                new Rule("lock", "Configs", new[]{"config"}, "settings"),
            },
            ["ryujinx"] = new List<Rule>
            {
                new Rule("junk", "Logs", new[]{"%DATA%\\Logs","Logs"}, "Ryujinx logs"),
                new Rule("opt", "Shader cache (disk)", new[]{"%LocalAppData%\\Ryujinx\\shader"}, "regenerates on next launch"),
                new Rule("lock", "Games & system data", new[]{"%DATA%\\bis","%DATA%\\sdcard","bis","sdcard"}, "saves, firmware data, games"),
                new Rule("lock", "Configs", new[]{"%DATA%\\Config.json"}, "settings"),
            },
            ["citralime"] = new List<Rule>
            {
                new Rule("junk", "Logs", new[]{"%DATA%\\log","log"}, "Citra/Lime logs"),
                new Rule("junk", "Cache", new[]{"%DATA%\\cache","cache"}, "shader cache"),
                new Rule("lock", "NAND & SD", new[]{"nand","sdmc"}, "saves and system data"),
                new Rule("lock", "Configs", new[]{"config","%DATA%\\config"}, "settings"),
            },
            ["ppsspp"] = new List<Rule>
            {
                new Rule("junk", "Logs", new[]{"log"}, "PPSSPP logs"),
                new Rule("junk", "Shader cache", new[]{"%DATA%\\cache","cache"}, "shader cache"),
                new Rule("lock", "memstick (saves, games)", new[]{"memstick","%DATA%\\memstick"}, "PSP saves and installed games"),
                new Rule("lock", "Configs", new[]{"%DATA%\\PSP\\SYSTEM"}, "settings"),
            },
            ["cemu"] = new List<Rule>
            {
                new Rule("junk", "Shader cache", new[]{"shaderCache"}, "transferable/precompiled shader cache"),
                new Rule("junk", "Logs & dumps", new[]{"log.txt","dump","crashdumps"}, "debug output"),
                new Rule("lock", "mlc01 (saves, updates, DLC)", new[]{"mlc01"}, "Wii U system data, saves"),
                new Rule("lock", "Game profiles & settings", new[]{"gameProfiles","config"}, "settings and profiles"),
            },
            ["xenia"] = new List<Rule>
            {
                new Rule("junk", "Logs", new[]{"xenia.log","logs"}, "Xenia logs"),
                new Rule("junk", "Cache", new[]{"cache"}, "shader cache"),
                new Rule("lock", "Content (saves)", new[]{"content"}, "game saves and installed content"),
            },
            ["duckstation"] = new List<Rule>
            {
                new Rule("junk", "Cache", new[]{"cache","%DATA%\\cache"}, "shader cache"),
                new Rule("junk", "Logs & dumps", new[]{"dump","%DATA%\\dump"}, "debug dumps"),
                new Rule("lock", "Memory cards", new[]{"memcards","%DATA%\\memcards"}, "PS1 memory cards"),
                new Rule("lock", "Save states", new[]{"savestates","%DATA%\\savestates"}, "your save states"),
                new Rule("lock", "BIOS", new[]{"bios","%DATA%\\bios"}, "required to run games"),
                new Rule("lock", "Configs", new[]{"settings","%DATA%\\settings"}, "settings"),
            },
            ["retroarch"] = new List<Rule>
            {
                new Rule("junk", "Logs", new[]{"logs"}, "RetroArch logs"),
                new Rule("junk", "Crash dumps", new[]{"crashdumps"}, "crash reports"),
                new Rule("opt", "Thumbnails", new[]{"thumbnails"}, "box art — re-downloads"),
                new Rule("lock", "Saves & states", new[]{"saves","states"}, "per-core saves and save states"),
                new Rule("lock", "System files (BIOS)", new[]{"system"}, "BIOS/firmware — required"),
                new Rule("lock", "Playlists & configs", new[]{"playlists","config"}, "libraries and settings"),
            },
            ["mgba"] = new List<Rule>
            {
                new Rule("junk", "Logs", new[]{"%DATA%\\log"}, "mGBA logs"),
                new Rule("junk", "Shader cache", new[]{"%DATA%\\cache","cache"}, "shader cache"),
                new Rule("lock", "Battery saves", new[]{".sav files next to ROMs"}, "in-ROM-folder saves"),
                new Rule("lock", "Configs", new[]{"config.ini","%DATA%\\config.ini"}, "settings"),
            },
            ["project64"] = new List<Rule>
            {
                new Rule("junk", "Logs", new[]{"%DATA%\\Logs","logs"}, "Project64 logs"),
                new Rule("lock", "Saves", new[]{"%DATA%\\Save","Save"}, "N64 saves"),
                new Rule("lock", "Configs", new[]{"Config","%DATA%\\Config"}, "settings and plugins config"),
            },
            ["vita3k"] = new List<Rule>
            {
                new Rule("junk", "Shader cache", new[]{"%DATA%\\cache","cache"}, "shader cache"),
                new Rule("junk", "Logs", new[]{"%DATA%\\log","log"}, "Vita3K logs"),
                new Rule("lock", "Games & firmware", new[]{"%DATA%\\ux0","ux0"}, "installed games, saves, firmware"),
                new Rule("lock", "Configs", new[]{"%DATA%\\config.yml","config.yml"}, "settings"),
            },
            ["xemu"] = new List<Rule>
            {
                new Rule("junk", "Logs & dumps", new[]{"%DATA%\\dumps","dumps","xemu.log"}, "debug output"),
                new Rule("lock", "HDD & EEPROM", new[]{"%DATA%\\xemu\\hdd","hdd"}, "Xbox hard disk image, EEPROM"),
                new Rule("lock", "Configs", new[]{"%DATA%\\xemu\\xemu.ini"}, "settings"),
            },
        };

        public static List<Rule> RulesFor(string emuId)
        {
            var outp = new List<Rule>();
            List<Rule> gen;
            if (Rules.TryGetValue("*", out gen)) outp.AddRange(gen);
            List<Rule> own;
            if (Rules.TryGetValue(emuId, out own)) outp.AddRange(own);
            return outp;
        }

        // ------------------------------------------------- registry scan
        private static List<KeyValuePair<string, string>> RegistryUninstall()
        {
            var found = new List<KeyValuePair<string, string>>();
            var keys = new[]
            {
                new KeyValuePair<RegistryKey, string>(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                new KeyValuePair<RegistryKey, string>(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                new KeyValuePair<RegistryKey, string>(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            };
            foreach (var pair in keys)
            {
                RegistryKey root = null;
                try { root = pair.Key.OpenSubKey(pair.Value); } catch { }
                if (root == null) continue;
                foreach (var child in root.GetSubKeyNames())
                {
                    RegistryKey k = null;
                    try
                    {
                        k = root.OpenSubKey(child);
                        var name = k.GetValue("DisplayName") as string;
                        var loc = k.GetValue("InstallLocation") as string ?? "";
                        if (!string.IsNullOrEmpty(name))
                            found.Add(new KeyValuePair<string, string>(name.ToLowerInvariant(), loc));
                    }
                    catch { }
                    finally { if (k != null) k.Dispose(); }
                }
                root.Dispose();
            }
            return found;
        }

        private static HashSet<string> RunningProcesses()
        {
            var set = new HashSet<string>();
            try
            {
                foreach (var p in Process.GetProcesses())
                    set.Add(p.ProcessName.ToLowerInvariant() + ".exe");
            }
            catch { }
            return set;
        }

        // ------------------------------------------------- detect
        public static List<DetectedEmu> Detect(bool onlyInstalled = true)
        {
            var uninstall = RegistryUninstall();
            var procs = RunningProcesses();
            var results = new List<DetectedEmu>();
            foreach (var emu in Emulators)
            {
                string install = "", data = "";
                bool installed = false, running = false;

                foreach (var tpl in emu.InstallPaths)
                {
                    var p = Expand(tpl);
                    if (Directory.Exists(p)) { install = install == "" ? p : install; installed = true; }
                }
                foreach (var tpl in emu.DataPaths)
                {
                    var p = Expand(tpl);
                    if (Directory.Exists(p) && data == "") data = p;
                }
                foreach (var kw in emu.UninstallKeywords)
                {
                    foreach (var pair in uninstall)
                    {
                        if (pair.Key.Contains(kw) && !string.IsNullOrEmpty(pair.Value)
                            && Directory.Exists(pair.Value))
                        {
                            if (install == "")
                                install = pair.Value;
                            installed = true;
                        }
                    }
                }
                foreach (var proc in emu.Processes)
                {
                    if (procs.Contains(proc.ToLowerInvariant())) { running = true; break; }
                }

                if (onlyInstalled && !installed && !running && data == "") continue;
                results.Add(new DetectedEmu
                {
                    Id = emu.Id, Name = emu.Name, Kind = emu.Kind,
                    Installed = installed, Running = running,
                    InstallRoot = install, DataRoot = data,
                });
            }
            return results;
        }
    }
}
