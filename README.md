# Flying Azure Screensaver

A Windows screensaver that flies Microsoft Azure chevron logos across the screen
at a diagonal, leaving fading motion-blur trails. Inspired by After Dark's
*Flying Toasters*. Multi-monitor aware, with a simple settings dialog.

## Requirements

- Windows 10/11
- .NET 10 SDK (to build)

## Build

```
dotnet build -c Release
```

This produces `src/FlyingAzure/bin/Release/net10.0-windows/FlyingAzure.scr`
(a copy of the built executable).

## Test

```
dotnet test
```

## Install

1. Build in Release.
2. Right-click `FlyingAzure.scr` → **Install**, **or** copy it to `C:\Windows\System32\`.
3. Open **Settings → Personalization → Lock screen → Screen saver**, pick
   **FlyingAzure**, and use **Settings…** to configure.

## Command-line contract

- `FlyingAzure.scr /s` — run full-screen on all monitors
- `FlyingAzure.scr /c` — open the settings dialog
- `FlyingAzure.scr /p <hwnd>` — render into the OS preview pane

## Configuration

Settings persist to `HKCU\Software\FlyingAzure`:

- **Number of logos**, **Speed**, **Logo size**, **Trail length**, **Background color**

## Logo asset

`assets/azure-chevron.svg` — the Azure "A" chevron mark, embedded at build time
and drawn as vector geometry (crisp at any size). Swap this file for any
single-color, straight-line SVG to change the mark.
