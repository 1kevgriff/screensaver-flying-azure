# Flying Azure Screensaver — Design

- **Date:** 2026-06-16
- **Status:** Approved (pending spec review)
- **Inspiration:** After Dark's *Flying Toasters* — a continuous diagonal stream of objects drifting across a black screen. The modern twist: each object leaves a **fading motion-blur trail**.

## 1. Summary

A Windows screensaver that flies multiple **Microsoft Azure** logos (the chevron "A" mark) across the screen at a shared diagonal angle. Previous positions gradually fade away each frame, producing comet-like trails. Ships as a standard `.scr` with a simple configuration dialog and full multi-monitor support.

## 2. Goals / Non-goals

**Goals**
- Standard `.scr` behavior: run (`/s`), configure (`/c`), preview (`/p <hwnd>`).
- Multi-monitor: independent full-coverage animation on every display.
- Fade-trail effect tunable from the config dialog.
- Authentic Azure chevron mark, crisp at any size (vector, not blurry raster).
- Testable core logic (arg parsing, settings, simulation) under xUnit.

**Non-goals (YAGNI)**
- No wing-flap animation frames (Flying Toasters had them; not requested).
- No "toast" secondary sprites.
- No sound.
- No per-monitor independent settings (one shared config across displays).
- No SVG curve support beyond what the chevron asset needs (straight lines only).

## 3. Tech stack

- **.NET 10**, **C# 14**, WinForms + GDI+ (`System.Drawing`).
- No SVG-rendering NuGet: the chevron asset is two straight-line paths, parsed once into a `System.Drawing.Drawing2D.GraphicsPath` and filled with a transform per draw.
- Output: single self-contained executable, post-build copied to `FlyingAzure.scr`.
- Settings persisted to the registry: `HKCU\Software\FlyingAzure`.

## 4. Logo asset

- Source: `assets/azure-chevron.svg` — the chevron-only Azure "A" mark
  (`viewBox 0 0 16 16`, fill `#00abec`, paths use only `M/L/H/V/h/v/z`).
  Originally `logos/azure-offical.svg` from the community `jdmsft/azure-svg` repo.
- Embedded as a project resource. At startup it is parsed into a normalized
  `GraphicsPath` (fit into a 1.0 unit box, centered on its centroid) so sprites
  scale/rotate around their center.
- Flat fill `#00abec` (a `SolidBrush`). Crispness comes from filling the path
  under a transform — no bitmap scaling artifacts.
- **Swappable:** any straight-line single-color SVG dropped in as the resource
  works without code changes. (Curves would need parser extension — out of scope.)

## 5. The `.scr` process contract (`Program.Main`)

Parsed by `CommandLineParser` in Core. Windows passes args case-insensitively and
sometimes glued (`/c:12345`). Behavior:

| Arg | Mode | Action |
|-----|------|--------|
| `/s` | Run | Full-screen animation on all monitors. |
| `/c`, `/c:<hwnd>`, *(none)* | Config | Show config dialog (modal to `<hwnd>` if given). |
| `/p <hwnd>` | Preview | Animate inside the supplied preview window handle. |

- Run mode: one borderless, `TopMost`, cursor-hidden form per `Screen.AllScreens`,
  each sized to its monitor `Bounds`. Exit triggers (on **any** form) close them all:
  key down, mouse button, or mouse move beyond a small dead-zone (ignore the tiny
  spurious move Windows emits at launch). `ExitCode 0`.
- Preview mode: a child form re-parented into `<hwnd>` via `SetParent`, sized to the
  parent's client rect, no exit-on-input (Windows owns the lifetime).

## 6. Animation & fade-trail rendering

Each animated surface (monitor form or preview host) owns:
- a back-buffer `Bitmap` sized to the surface, and
- a `System.Windows.Forms.Timer` (or a tight `Application.Idle` pump) targeting ~60 FPS.

**Per frame:**
1. **Fade step** — fill the entire back-buffer with the background color at a low
   alpha (`SolidBrush(Color.FromArgb(fadeAlpha, bgColor))`). High alpha → short trail;
   low alpha → long trail. This is the only mechanism that clears prior frames, so
   prior sprite positions decay smoothly toward the background = the trail.
2. **Update** — advance every sprite (`Simulation.Step(dt)`).
3. **Draw** — for each sprite: set the buffer `Graphics` transform (translate to
   position, rotate to the travel angle, scale to the sprite size), fill the shared
   chevron `GraphicsPath`, reset transform. `SmoothingMode = AntiAlias`.
4. **Blit** — draw the back-buffer to the form in `OnPaint`; the timer tick calls
   `Invalidate()`.

Background defaults to **black** (Flying Toasters homage).

## 7. Simulation (`FlyingAzure.Core`)

- `Sprite`: `PointF Position`, `float Size`, `float Speed` (px/sec along the angle).
- `Simulation`:
  - Holds surface size, shared travel `Angle` (default ≈ 210° — heading down-left,
    the classic Flying Toasters diagonal), sprite list, and an injected RNG (seedable
    for deterministic tests).
  - `Step(double dtSeconds)`: moves each sprite `Speed*dt` along `Angle`.
  - **Wrap/respawn:** when a sprite's bounding box fully exits the surface, respawn it
    just off the opposite (upstream) edge at a random cross-axis offset, with a fresh
    random size within the configured min/max (depth variation). The field stays full
    and continuous — no gaps, no synchronized "reset."
  - Initial population scatters sprites across the surface (not all at one edge).
- Pure, deterministic given a seed → unit-testable without any GDI.

## 8. Configuration dialog (`ConfigForm`) — "Standard set"

Controls, each bound to a setting:
- **Logo count** (e.g. 1–60).
- **Speed** (slider; maps to px/sec).
- **Size** (slider; base size + the min/max variance band derived from it).
- **Trail length** (slider; maps inversely to `fadeAlpha` — left = short, right = long).
- **Background color** (color picker; default black).
- Optional small **live preview** panel running the same render path.
- **OK** saves to registry and closes; **Cancel** discards.

`SettingsStore` (Core): `Load()` returns a `Settings` record with sane defaults when
keys are absent; `Save(settings)` writes them. Registry I/O is isolated behind an
`ISettingsBackend` so the round-trip logic is testable with an in-memory backend.

## 9. Project structure

```
FlyingAzure.sln
 ├─ src/FlyingAzure.Core/        (class library, net10.0)
 │   ├─ CommandLineParser.cs     → ScreensaverMode + hwnd
 │   ├─ Settings.cs              → record + defaults
 │   ├─ SettingsStore.cs         → Load/Save over ISettingsBackend
 │   ├─ RegistrySettingsBackend.cs
 │   ├─ SvgPathParser.cs         → SVG "d" (M/L/H/V/h/v/z) → GraphicsPath
 │   ├─ Sprite.cs
 │   └─ Simulation.cs
 ├─ src/FlyingAzure/             (WinForms exe, net10.0-windows)
 │   ├─ Program.cs               → Main: parse args → dispatch
 │   ├─ ScreensaverForm.cs       → full-screen render + exit handling
 │   ├─ PreviewHost.cs           → /p hosting
 │   ├─ ConfigForm.cs            → settings UI
 │   ├─ ChevronRenderer.cs       → builds shared GraphicsPath + brush, draws sprites
 │   └─ assets/azure-chevron.svg (embedded resource)
 └─ tests/FlyingAzure.Core.Tests/ (xUnit, net10.0)
     ├─ CommandLineParserTests.cs
     ├─ SettingsStoreTests.cs
     ├─ SvgPathParserTests.cs
     └─ SimulationTests.cs
```

`System.Drawing.Common` is Windows-only on .NET 10; both the exe and Core target
Windows-capable TFMs. `GraphicsPath` construction lives in Core but is only exercised
on Windows test runs.

## 10. Testing strategy

TDD on the pure Core:
- **CommandLineParser:** `/s`, `/c`, `/c:123`, `/p 456`, none, unknown, case-insensitive.
- **SettingsStore:** defaults on empty backend; save→load round-trip; clamp out-of-range.
- **SvgPathParser:** parses the chevron `d` into the expected point set; handles abs+rel
  `M/L/H/V/h/v/z`; rejects unsupported commands clearly.
- **Simulation:** sprite moves along angle by `speed*dt`; respawns upstream after fully
  exiting; deterministic with a fixed seed; population count honored.

Rendering (`ScreensaverForm`, `ChevronRenderer`, `ConfigForm`) is a thin shell verified
by running the actual screensaver; no pixel-level unit tests.

## 11. Build & install

- `dotnet build` / `dotnet test` from the solution.
- Post-build (Release) copies `FlyingAzure.exe` → `FlyingAzure.scr`.
- Install: right-click `FlyingAzure.scr` → **Install**, or copy to
  `C:\Windows\System32`. README documents both, plus the dev workflow.

## 12. Risks / open considerations

- **Spurious startup mouse-move** closing run mode instantly → use a first-move
  baseline + dead-zone before treating movement as user activity.
- **Multi-monitor coordinates** → each form uses its own `Screen.Bounds`; the
  simulation works in surface-local coordinates per form.
- **Preview-window lifetime** (`/p`) → watch for the parent handle going invalid; stop
  the timer and exit when the parent closes.
- **High-DPI** → set per-monitor-v2 DPI awareness so full-screen bounds are correct.
