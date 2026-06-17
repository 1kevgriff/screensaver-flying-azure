# Flying Azure Screensaver

A cross-platform (Windows + macOS) screensaver that flies Microsoft Azure chevron
logos across the screen at a diagonal, leaving fading motion-blur trails — some
logos faster than others. Inspired by After Dark's *Flying Toasters*. Multi-monitor
aware, with a settings dialog and an optional culture-formatted clock. Windows and
macOS share one SkiaSharp render engine (compiled to a native library via NativeAOT).

## Download

Grab the latest builds from the [**Releases**](https://github.com/1kevgriff/screensaver-flying-azure/releases/latest) page:

| Platform | File | Notes |
|---|---|---|
| Windows | `FlyingAzure-Setup-x64.exe` | One-click installer — adds it to Screen Saver Settings |
| Windows | `FlyingAzure-Screensaver-win-x64.zip` | Portable — unzip and run `install.ps1` |
| macOS (Apple Silicon) | `FlyingAzure-osx-arm64.saver.zip` | Signed & notarized — unzip, double-click the `.saver` to install |

The Windows release builds are self-contained (no .NET runtime required). The macOS
build is signed with a Developer ID and notarized by Apple, so it opens without
Gatekeeper warnings.

## Requirements

- Windows 10/11
- .NET 10 SDK (to build)

## Build & package

A `.scr` must be a standalone, runnable apphost — not the managed `.dll` — so the
screensaver is produced by a **single-file publish**, not by plain `dotnet build`:

```bash
dotnet build -c Release   # compile (and `dotnet test` runs the unit tests)
dotnet publish src/FlyingAzure -c Release -r win-x64 -p:SelfContained=false -p:PublishSingleFile=true
```

The single-file screensaver lands at
`src/FlyingAzure/bin/Release/net10.0-windows/win-x64/publish/FlyingAzure.scr`.
It is framework-dependent (needs the **.NET 10 Desktop Runtime**); add
`-p:SelfContained=true` for a fully standalone (larger) build. `install.ps1` runs
this publish for you, so you normally don't invoke it by hand.

## Test

```bash
dotnet test
```

## Install

### Scripted (recommended)

```powershell
./install.ps1          # builds, installs per-user, activates (10-min idle timeout)
./uninstall.ps1        # removes it and deactivates
```

`install.ps1` (per-user, no elevation) copies `FlyingAzure.scr` to
`%LOCALAPPDATA%\FlyingAzure` and sets it as your active screensaver. Useful
switches:

- `-System` — install into `C:\Windows\System32` so it shows up in the Windows
  screensaver drop-down list (run PowerShell **as Administrator**).
- `-TimeoutMinutes <n>` — idle minutes before it starts (default 10).
- `-NoActivate` — install without changing your current screensaver.
- `-NoBuild` — use the existing Release build instead of rebuilding.

`uninstall.ps1` accepts `-System` (remove the System32 copy, elevated) and
`-KeepSettings` (leave `HKCU\Software\FlyingAzure` in place).

### Manual

1. Publish the single-file `.scr` (see **Build & package**).
2. Right-click `FlyingAzure.scr` → **Install**, **or** copy it to `C:\Windows\System32\` (64-bit Windows; the right-click → Install option works regardless).
3. Open **Settings → Personalization → Lock screen → Screen saver**, pick
   **FlyingAzure**, and use **Settings…** to configure.

## Command-line contract

- `FlyingAzure.scr /s` — run full-screen on all monitors
- `FlyingAzure.scr /c` — open the settings dialog
- `FlyingAzure.scr /p <hwnd>` — render into the OS preview pane

## Configuration

Settings persist to `HKCU\Software\FlyingAzure`:

- **Number of logos**, **Speed**, **Logo size**, **Trail length**, **Background color**,
  **Date / time** clock corner (off / any corner; shown on every monitor, formatted in your culture)

## Logo asset

`assets/azure-chevron.svg` — the Azure "A" chevron mark, embedded at build time
and drawn as vector geometry (crisp at any size). Swap this file for any
single-color, straight-line SVG to change the mark.
