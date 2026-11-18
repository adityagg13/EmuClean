<div align="center">

# 🧹 EmuClean

**Emulator Debloater & Optimizer — ek hi kaam, achhe se.**

Auto-detects your installed emulators, cleans their junk **safely into the Recycle Bin**,
and optimizes Android + PC settings for them.

[![Release](https://img.shields.io/github/v/release/adityagg13/EmuClean?style=for-the-badge&color=4f8dff)](https://github.com/adityagg13/EmuClean/releases/latest)
[![Platform](https://img.shields.io/badge/platform-Windows_10_11-0078d4?style=for-the-badge&logo=windows)](https://github.com/adityagg13/EmuClean/releases/latest)
[![.NET](https://img.shields.io/badge/.NET_Framework-4.8-5C2D91?style=for-the-badge&logo=dotnet)](https://github.com/adityagg13/EmuClean)
[![License](https://img.shields.io/github/license/adityagg13/EmuClean?style=for-the-badge&color=3ecf8e)](LICENSE)

### 📥 [Download the latest EXE →](https://github.com/adityagg13/EmuClean/releases/latest)

*Extract the zip → run `EmuClean.exe` → allow the UAC prompt. That's it.*

</div>

---

## What it does

| Mode | Description |
|---|---|
| 🧹 **Clean** | Finds **junk only** (logs, cache, shader cache, crash dumps) for every detected emulator — auto-selected and pre-sized. **Saves, BIOS, memory cards and instance disks stay locked 🔒 and are never auto-touched.** Every delete goes to the **Recycle Bin**, so a mistake is always reversible. Also scans for **leftover folders** from uninstalled emulators. |
| 🤖 **ADB Deep Clean** | For BlueStacks: connects over `HD-Adb.exe` and removes **preinstalled junk packages** with three profiles (Safe / Balanced / Aggressive). A hard **core safe-list** (phone, settings, GMS, launcher…) can never be selected. |
| 📱 **Device Mode** | Your **real phone or tablet over USB** — Device Info, Files Transfer, **BloatDroid** (list / interactive uninstall / **Full Nuke** of known preinstalled bloat), Force Stop ALL apps, per-app notification deny, Reboot / Recovery. Reversible (`--user 0` removals + one-click restore). Inspired by **EVGA CoreUP** by [@xelor7](https://github.com/xelor7). |
| ⚡ **Optimize** | Android (over ADB): cached-apps freezer, activity logging off, package verifier off… PC: **High Performance power plan**, **GameDVR off**, **High priority** for running emulator processes. |

## Screenshots

| | |
|---|---|
| ![Clean page](screenshots/clean-page.png) | ![Rules with locked user data](screenshots/rules-view.png) |
| *Detected emulators + junk size* | *Junk auto-checked, user data 🔒 locked* |

![Optimize page](screenshots/optimize.png)
*Android + PC optimization toggles*

## Supported emulators (25)

**Android:** BlueStacks 4/5 · LDPlayer 9 · NoxPlayer · MEmu · Gameloop · MuMu Player · KOPlayer · Genymotion
**Console/desktop:** Dolphin · PCSX2 · RPCS3 · Yuzu · Suyu · Ryujinx · Lime3DS/Citra · PPSSPP · Cemu · Xenia · DuckStation · RetroArch · mGBA · Project64 · Vita3K · xemu

## Device Mode (v1.1.0) — your phone, CoreUP style

Plug in any Android phone, enable **USB debugging** (Developer options), open the **📱 Device Mode** page:

- **Device Info** — model, Android version, security patch, battery, storage, RAM
- **Files Transfer** — one-click pull of Screenshots / Camera / Downloads to `Downloads\EmuClean`, or browse `/sdcard` and push files to the phone
- **🧟 BloatDroid** — list packages (3rd-party + system), interactive uninstall, or **☢️ Full Nuke**: one click removes ~26 known-bloat families (Facebook, TikTok, preinstalled trial games, shopping apps…) for the current user only — every removal is restorable
- **Quick Actions** — 🧊 Force Stop ALL 3rd-party apps · 🔕 Disable notifications for ALL apps (`appops POST_NOTIFICATION`, Android 9+) · Reboot / Reboot to recovery
- `adb.exe` (Android platform-tools) is **bundled in the release zip** — no SDK install needed

## Safety design

- **Junk-only auto selection** — the engine separates regenerable junk from user data; only `junk` items are auto-checked
- 🔒 **Locked categories** — saves/memcards/BIOS/instances are shown with a lock, never auto-selected, and warn if you check them
- ♻️ **Recycle Bin deletes** — `SHFileOperation`-level recycle, so "clean" is reversible until you empty the bin
- 🛡️ **Core Android safe-list** — 22 system packages are hard-blocked from removal
- UAC admin is requested up-front (folder cleaning + ADB need it)
- Device Mode removals use `pm uninstall --user 0` — the app stays on the system partition and can be brought back with **Restore** (`cmd package install-existing`)

## Requirements

- Windows 10 / 11 (`.NET Framework 4.8` is built into both)
- **WebView2 Runtime** — preinstalled on Windows 11 and most Windows 10 installs; if missing, grab it from [Microsoft](https://developer.microsoft.com/microsoft-edge/webview2/)
- BlueStacks must be **running** for the ADB page
- Device Mode: any Android phone with **USB debugging** enabled (adb.exe bundled in the zip)

## Build from source

```bash
dotnet build -c Release          # or: dotnet publish -c Release
# output: EmuClean/bin/Release/publish/  (EmuClean.exe + ui/)
```

Project layout:

```
EmuClean/
├── EmuClean.csproj      net48 + WebView2 host
├── app.manifest         requireAdministrator
├── Program.cs           entry point
├── Host.cs              frameless WebView2 form + JS↔C# bridge
└── Engine/
    ├── Detector.cs      25 emulators: paths, registry, processes
    ├── Cleaner.cs       junk scan, sizing, recycle-safe delete
    ├── AdbManager.cs    BlueStacks ADB + junk profiles + safe-list
    ├── DeviceManager.cs Device Mode: info, BloatDroid/Full Nuke, transfer, quick actions
    ├── Optimizer.cs     android + pc tweaks
    └── Api.cs           bridge surface used by the UI
ui/                      HTML/CSS/JS interface (works standalone as a preview)
```

## License

MIT — see [LICENSE](LICENSE).
