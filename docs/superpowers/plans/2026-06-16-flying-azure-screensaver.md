# Flying Azure Screensaver Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows `.scr` screensaver that flies Azure chevron logos across the screen at a shared diagonal angle, leaving fading motion-blur trails, with a config dialog and multi-monitor support.

**Architecture:** A pure, testable `FlyingAzure.Core` class library (arg parsing, settings, SVG-path parsing, path normalization, sprite simulation) plus a thin WinForms shell (`FlyingAzure`) that renders with GDI+ and implements the standard screensaver process contract (`/s`, `/c`, `/p`). The fade trail comes from painting a low-alpha background rectangle over an accumulation buffer each frame before drawing sprites.

**Tech Stack:** .NET 10, C# 14, WinForms + GDI+ (`System.Drawing`), xUnit. No SVG-rendering NuGet — the chevron is straight-line paths parsed into a `GraphicsPath`.

## Global Constraints

- Target frameworks: `FlyingAzure.Core` → `net10.0`; `FlyingAzure` → `net10.0-windows`; `FlyingAzure.Core.Tests` → `net10.0-windows`.
- Every project: `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- `FlyingAzure` project: `<OutputType>WinExe</OutputType>`, `<UseWindowsForms>true</UseWindowsForms>`.
- Logo color is exactly `#00ABEC` → `Color.FromArgb(0x00, 0xAB, 0xEC)`.
- Default background is black; default shared travel angle is `150f` degrees (down-left in screen coordinates where +x = right, +y = down).
- Logos stay **upright** — the travel angle affects motion only, never sprite orientation.
- Settings persist under registry key `HKCU\Software\FlyingAzure`.
- After editing any `.cs`, run `dotnet build`; if a project has tests, run `dotnet test` (per user .NET rules).
- Git commits MUST NOT include any `Co-Authored-By: Claude` trailer.
- Run all `dotnet` and `git` commands from the repo root `K:\screensavers\flying-azure-screensaver`.

---

### Task 1: Solution and project scaffold

**Files:**
- Create: `FlyingAzure.sln`
- Create: `src/FlyingAzure.Core/FlyingAzure.Core.csproj`
- Create: `src/FlyingAzure/FlyingAzure.csproj`
- Create: `tests/FlyingAzure.Core.Tests/FlyingAzure.Core.Tests.csproj`
- Create: `Directory.Build.props`

**Interfaces:**
- Consumes: nothing.
- Produces: a buildable 3-project solution. Project names `FlyingAzure.Core`, `FlyingAzure`, `FlyingAzure.Core.Tests`.

- [ ] **Step 1: Create the solution and projects**

```bash
cd "K:/screensavers/flying-azure-screensaver"
dotnet new sln -n FlyingAzure
dotnet new classlib -n FlyingAzure.Core -o src/FlyingAzure.Core -f net10.0
dotnet new winforms -n FlyingAzure -o src/FlyingAzure -f net10.0-windows
dotnet new xunit -n FlyingAzure.Core.Tests -o tests/FlyingAzure.Core.Tests -f net10.0-windows
rm -f src/FlyingAzure.Core/Class1.cs
dotnet sln add src/FlyingAzure.Core src/FlyingAzure tests/FlyingAzure.Core.Tests
dotnet add src/FlyingAzure reference src/FlyingAzure.Core
dotnet add tests/FlyingAzure.Core.Tests reference src/FlyingAzure.Core
dotnet add tests/FlyingAzure.Core.Tests reference src/FlyingAzure
```

- [ ] **Step 2: Write `Directory.Build.props`**

Create `Directory.Build.props` at repo root:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Delete the WinForms template's default Form**

The `winforms` template creates `Form1.cs`/`Form1.Designer.cs` and a `Program.cs` we will replace later. Remove the default form now; keep `Program.cs` for now (replaced in Task 8).

```bash
rm -f src/FlyingAzure/Form1.cs src/FlyingAzure/Form1.Designer.cs src/FlyingAzure/Form1.resx
```

- [ ] **Step 4: Replace template `Program.cs` with a placeholder**

Overwrite `src/FlyingAzure/Program.cs`:

```csharp
namespace FlyingAzure;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Replaced in Task 8 with full dispatch.
        return 0;
    }
}
```

- [ ] **Step 5: Build the solution**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: scaffold solution, core lib, winforms app, test project"
```

---

### Task 2: Command-line parser

**Files:**
- Create: `src/FlyingAzure.Core/CommandLineParser.cs`
- Test: `tests/FlyingAzure.Core.Tests/CommandLineParserTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `enum ScreensaverMode { Run, Configure, Preview }`
  - `readonly record struct ParsedArgs(ScreensaverMode Mode, nint WindowHandle)`
  - `static class CommandLineParser { static ParsedArgs Parse(string[] args); }`

- [ ] **Step 1: Write the failing tests**

Create `tests/FlyingAzure.Core.Tests/CommandLineParserTests.cs`:

```csharp
using FlyingAzure.Core;
using Xunit;

namespace FlyingAzure.Core.Tests;

public class CommandLineParserTests
{
    [Fact]
    public void NoArgs_IsConfigure() =>
        Assert.Equal(ScreensaverMode.Configure, CommandLineParser.Parse([]).Mode);

    [Theory]
    [InlineData("/s")]
    [InlineData("-s")]
    [InlineData("/S")]
    public void SFlag_IsRun(string arg) =>
        Assert.Equal(ScreensaverMode.Run, CommandLineParser.Parse([arg]).Mode);

    [Theory]
    [InlineData("/c")]
    [InlineData("/C")]
    public void CFlag_IsConfigure(string arg) =>
        Assert.Equal(ScreensaverMode.Configure, CommandLineParser.Parse([arg]).Mode);

    [Fact]
    public void CFlagWithColonHandle_ParsesHandle()
    {
        var p = CommandLineParser.Parse(["/c:12345"]);
        Assert.Equal(ScreensaverMode.Configure, p.Mode);
        Assert.Equal((nint)12345, p.WindowHandle);
    }

    [Fact]
    public void PFlagWithSpaceHandle_IsPreviewWithHandle()
    {
        var p = CommandLineParser.Parse(["/p", "67890"]);
        Assert.Equal(ScreensaverMode.Preview, p.Mode);
        Assert.Equal((nint)67890, p.WindowHandle);
    }

    [Fact]
    public void UnknownArg_IsConfigure() =>
        Assert.Equal(ScreensaverMode.Configure, CommandLineParser.Parse(["whatever"]).Mode);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter CommandLineParserTests`
Expected: FAIL — `CommandLineParser` / `ScreensaverMode` do not exist.

- [ ] **Step 3: Write the implementation**

Create `src/FlyingAzure.Core/CommandLineParser.cs`:

```csharp
namespace FlyingAzure.Core;

public enum ScreensaverMode
{
    Run,
    Configure,
    Preview,
}

public readonly record struct ParsedArgs(ScreensaverMode Mode, nint WindowHandle);

public static class CommandLineParser
{
    public static ParsedArgs Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new ParsedArgs(ScreensaverMode.Configure, 0);
        }

        string first = args[0].Trim();
        string flag = (first.Length >= 2 ? first[..2] : first).ToLowerInvariant();

        return flag switch
        {
            "/s" or "-s" => new ParsedArgs(ScreensaverMode.Run, 0),
            "/p" or "-p" => new ParsedArgs(ScreensaverMode.Preview, ParseHandle(first, args)),
            "/c" or "-c" => new ParsedArgs(ScreensaverMode.Configure, ParseHandle(first, args)),
            _ => new ParsedArgs(ScreensaverMode.Configure, 0),
        };
    }

    private static nint ParseHandle(string first, string[] args)
    {
        int colon = first.IndexOf(':');
        if (colon >= 0 && long.TryParse(first[(colon + 1)..], out long inline))
        {
            return (nint)inline;
        }

        if (args.Length >= 2 && long.TryParse(args[1], out long separate))
        {
            return (nint)separate;
        }

        return 0;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter CommandLineParserTests`
Expected: PASS (all cases).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add screensaver command-line parser"
```

---

### Task 3: Settings, value mapping, and persistence

**Files:**
- Create: `src/FlyingAzure.Core/Settings.cs`
- Create: `src/FlyingAzure.Core/ISettingsBackend.cs`
- Create: `src/FlyingAzure.Core/SettingsStore.cs`
- Create: `src/FlyingAzure.Core/RegistrySettingsBackend.cs`
- Test: `tests/FlyingAzure.Core.Tests/SettingsTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `record Settings` with `int LogoCount, Speed, Size, TrailLength; int BackgroundArgb;` `static Settings Default`; `Settings Clamp()`; `int SpeedPixelsPerSecond()`; `float BaseSizePixels()`; `int FadeAlpha()`; `Color BackgroundColor()`.
  - `interface ISettingsBackend { string? Read(string key); void Write(string key, string value); }`
  - `class SettingsStore(ISettingsBackend backend) { Settings Load(); void Save(Settings s); }`
  - `class RegistrySettingsBackend : ISettingsBackend` (registry under `HKCU\Software\FlyingAzure`).

- [ ] **Step 1: Write the failing tests**

Create `tests/FlyingAzure.Core.Tests/SettingsTests.cs`:

```csharp
using System.Drawing;
using FlyingAzure.Core;
using Xunit;

namespace FlyingAzure.Core.Tests;

public sealed class InMemoryBackend : ISettingsBackend
{
    private readonly Dictionary<string, string> _data = new();
    public string? Read(string key) => _data.TryGetValue(key, out var v) ? v : null;
    public void Write(string key, string value) => _data[key] = value;
}

public class SettingsTests
{
    [Fact]
    public void Load_EmptyBackend_ReturnsDefaults()
    {
        var store = new SettingsStore(new InMemoryBackend());
        Assert.Equal(Settings.Default, store.Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var backend = new InMemoryBackend();
        var store = new SettingsStore(backend);
        var s = Settings.Default with { LogoCount = 33, Speed = 70, Size = 40, TrailLength = 90, BackgroundArgb = unchecked((int)0xFF102030) };
        store.Save(s);
        Assert.Equal(s, store.Load());
    }

    [Fact]
    public void Clamp_ConstrainsRanges()
    {
        var s = new Settings { LogoCount = 9999, Speed = -5, Size = 200, TrailLength = 50, BackgroundArgb = 0 }.Clamp();
        Assert.Equal(80, s.LogoCount);
        Assert.Equal(0, s.Speed);
        Assert.Equal(100, s.Size);
    }

    [Fact]
    public void SpeedMapping_Monotonic()
    {
        Assert.True((Settings.Default with { Speed = 0 }).SpeedPixelsPerSecond()
            < (Settings.Default with { Speed = 100 }).SpeedPixelsPerSecond());
    }

    [Fact]
    public void FadeAlpha_LongerTrail_LowerAlpha()
    {
        Assert.True((Settings.Default with { TrailLength = 100 }).FadeAlpha()
            < (Settings.Default with { TrailLength = 0 }).FadeAlpha());
    }

    [Fact]
    public void BackgroundColor_RoundTripsArgb()
    {
        var c = (Settings.Default with { BackgroundArgb = unchecked((int)0xFF112233) }).BackgroundColor();
        Assert.Equal(Color.FromArgb(unchecked((int)0xFF112233)), c);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter SettingsTests`
Expected: FAIL — `Settings` / `SettingsStore` do not exist.

- [ ] **Step 3: Write `Settings.cs`**

Create `src/FlyingAzure.Core/Settings.cs`:

```csharp
using System.Drawing;

namespace FlyingAzure.Core;

public sealed record Settings
{
    public int LogoCount { get; init; } = 24;
    public int Speed { get; init; } = 50;        // 0..100
    public int Size { get; init; } = 50;          // 0..100
    public int TrailLength { get; init; } = 55;   // 0..100 (higher = longer trail)
    public int BackgroundArgb { get; init; } = unchecked((int)0xFF000000); // opaque black

    public static Settings Default => new();

    public Settings Clamp() => this with
    {
        LogoCount = Math.Clamp(LogoCount, 1, 80),
        Speed = Math.Clamp(Speed, 0, 100),
        Size = Math.Clamp(Size, 0, 100),
        TrailLength = Math.Clamp(TrailLength, 0, 100),
    };

    public int SpeedPixelsPerSecond() => 30 + (int)Math.Round(Math.Clamp(Speed, 0, 100) / 100.0 * 270);

    public float BaseSizePixels() => 28f + Math.Clamp(Size, 0, 100) / 100f * 172f;

    // Longer trail => lower per-frame fade alpha. Floor of 6 keeps the buffer from never clearing.
    public int FadeAlpha() => 6 + (int)Math.Round((100 - Math.Clamp(TrailLength, 0, 100)) / 100.0 * 54);

    public Color BackgroundColor() => Color.FromArgb(BackgroundArgb);
}
```

- [ ] **Step 4: Write `ISettingsBackend.cs`**

Create `src/FlyingAzure.Core/ISettingsBackend.cs`:

```csharp
namespace FlyingAzure.Core;

public interface ISettingsBackend
{
    string? Read(string key);
    void Write(string key, string value);
}
```

- [ ] **Step 5: Write `SettingsStore.cs`**

Create `src/FlyingAzure.Core/SettingsStore.cs`:

```csharp
using System.Globalization;

namespace FlyingAzure.Core;

public sealed class SettingsStore(ISettingsBackend backend)
{
    public Settings Load()
    {
        var d = Settings.Default;
        return new Settings
        {
            LogoCount = ReadInt(nameof(Settings.LogoCount), d.LogoCount),
            Speed = ReadInt(nameof(Settings.Speed), d.Speed),
            Size = ReadInt(nameof(Settings.Size), d.Size),
            TrailLength = ReadInt(nameof(Settings.TrailLength), d.TrailLength),
            BackgroundArgb = ReadInt(nameof(Settings.BackgroundArgb), d.BackgroundArgb),
        }.Clamp();
    }

    public void Save(Settings s)
    {
        s = s.Clamp();
        Write(nameof(Settings.LogoCount), s.LogoCount);
        Write(nameof(Settings.Speed), s.Speed);
        Write(nameof(Settings.Size), s.Size);
        Write(nameof(Settings.TrailLength), s.TrailLength);
        Write(nameof(Settings.BackgroundArgb), s.BackgroundArgb);
    }

    private int ReadInt(string key, int fallback) =>
        int.TryParse(backend.Read(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

    private void Write(string key, int value) =>
        backend.Write(key, value.ToString(CultureInfo.InvariantCulture));
}
```

- [ ] **Step 6: Write `RegistrySettingsBackend.cs`**

Create `src/FlyingAzure.Core/RegistrySettingsBackend.cs`:

```csharp
using Microsoft.Win32;

namespace FlyingAzure.Core;

public sealed class RegistrySettingsBackend(string subKey = @"Software\FlyingAzure") : ISettingsBackend
{
    public string? Read(string key)
    {
        using var reg = Registry.CurrentUser.OpenSubKey(subKey);
        return reg?.GetValue(key)?.ToString();
    }

    public void Write(string key, string value)
    {
        using var reg = Registry.CurrentUser.CreateSubKey(subKey);
        reg.SetValue(key, value);
    }
}
```

The `Microsoft.Win32.Registry` type is available on `net10.0` via the Windows-only `Microsoft.Win32.Registry` package included in the SDK; if the build reports it missing, add the package: `dotnet add src/FlyingAzure.Core package Microsoft.Win32.Registry`.

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test --filter SettingsTests`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: add settings model, value mapping, and registry persistence"
```

---

### Task 4: SVG path parser

**Files:**
- Create: `src/FlyingAzure.Core/SvgPathParser.cs`
- Test: `tests/FlyingAzure.Core.Tests/SvgPathParserTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `static class SvgPathParser { static IReadOnlyList<IReadOnlyList<PointF>> Parse(string d); }`. Supports `M m L l H h V v Z z`; treats coordinate pairs after `M`/`m` as implicit line-to; throws `FormatException` on unsupported commands.

- [ ] **Step 1: Write the failing tests**

Create `tests/FlyingAzure.Core.Tests/SvgPathParserTests.cs`:

```csharp
using System.Drawing;
using FlyingAzure.Core;
using Xunit;

namespace FlyingAzure.Core.Tests;

public class SvgPathParserTests
{
    private static void AssertPoint(PointF expected, PointF actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
    }

    [Fact]
    public void Parse_AbsoluteSquare_ReturnsFourPoints()
    {
        var result = SvgPathParser.Parse("M0 0 L2 0 L2 2 L0 2 Z");
        var sub = Assert.Single(result);
        Assert.Equal(4, sub.Count);
        AssertPoint(new PointF(0, 0), sub[0]);
        AssertPoint(new PointF(2, 0), sub[1]);
        AssertPoint(new PointF(2, 2), sub[2]);
        AssertPoint(new PointF(0, 2), sub[3]);
    }

    [Fact]
    public void Parse_HandlesHVAndRelative()
    {
        // M then H (abs horiz), then relative v
        var result = SvgPathParser.Parse("M1 1 H5 v3");
        var sub = Assert.Single(result);
        AssertPoint(new PointF(1, 1), sub[0]);
        AssertPoint(new PointF(5, 1), sub[1]);
        AssertPoint(new PointF(5, 4), sub[2]);
    }

    [Fact]
    public void Parse_Chevron_ProducesTwoSubpaths()
    {
        const string d = "M3.65 14.2H16L9.35 2.68 7.33 8.24l3.88 4.63-7.56 1.33zM8.82 1.8L4.07 5.79 0 12.84h3.67v.01L8.82 1.8z";
        var result = SvgPathParser.Parse(d);
        Assert.Equal(2, result.Count);
        AssertPoint(new PointF(3.65f, 14.2f), result[0][0]);
        AssertPoint(new PointF(16f, 14.2f), result[0][1]);
        AssertPoint(new PointF(9.35f, 2.68f), result[0][2]);
        AssertPoint(new PointF(7.33f, 8.24f), result[0][3]);
        AssertPoint(new PointF(11.21f, 12.87f), result[0][4]);
        AssertPoint(new PointF(3.65f, 14.2f), result[0][5]);
        AssertPoint(new PointF(8.82f, 1.8f), result[1][0]);
        AssertPoint(new PointF(0f, 12.84f), result[1][2]);
        AssertPoint(new PointF(3.67f, 12.84f), result[1][3]);
        AssertPoint(new PointF(3.67f, 12.85f), result[1][4]);
    }

    [Fact]
    public void Parse_UnsupportedCommand_Throws() =>
        Assert.Throws<FormatException>(() => SvgPathParser.Parse("M0 0 C1 1 2 2 3 3"));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter SvgPathParserTests`
Expected: FAIL — `SvgPathParser` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/FlyingAzure.Core/SvgPathParser.cs`:

```csharp
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FlyingAzure.Core;

public static partial class SvgPathParser
{
    [GeneratedRegex(@"[MLHVZmlhvz]|-?\d*\.?\d+(?:[eE][-+]?\d+)?")]
    private static partial Regex Token();

    public static IReadOnlyList<IReadOnlyList<PointF>> Parse(string d)
    {
        var tokens = Token().Matches(d).Select(m => m.Value).ToList();
        var subpaths = new List<List<PointF>>();
        List<PointF>? current = null;
        float x = 0, y = 0, startX = 0, startY = 0;
        char cmd = '\0';
        int i = 0;

        float Num() => float.Parse(tokens[i++], CultureInfo.InvariantCulture);
        static bool IsCmd(string t) => t.Length == 1 && "MLHVZmlhvz".Contains(t[0]);

        while (i < tokens.Count)
        {
            if (IsCmd(tokens[i]))
            {
                cmd = tokens[i][0];
                i++;
            }

            switch (cmd)
            {
                case 'M':
                case 'm':
                    float mx = Num(), my = Num();
                    if (cmd == 'm' && current is not null) { mx += x; my += y; }
                    x = mx; y = my; startX = x; startY = y;
                    current = [new PointF(x, y)];
                    subpaths.Add(current);
                    cmd = cmd == 'M' ? 'L' : 'l';
                    break;
                case 'L':
                case 'l':
                    float lx = Num(), ly = Num();
                    if (cmd == 'l') { lx += x; ly += y; }
                    x = lx; y = ly;
                    current!.Add(new PointF(x, y));
                    break;
                case 'H':
                case 'h':
                    float hx = Num();
                    if (cmd == 'h') hx += x;
                    x = hx;
                    current!.Add(new PointF(x, y));
                    break;
                case 'V':
                case 'v':
                    float vy = Num();
                    if (cmd == 'v') vy += y;
                    y = vy;
                    current!.Add(new PointF(x, y));
                    break;
                case 'Z':
                case 'z':
                    x = startX; y = startY;
                    current = null;
                    cmd = '\0';
                    break;
                default:
                    throw new FormatException($"Unsupported SVG path command '{cmd}'.");
            }
        }

        return subpaths;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter SvgPathParserTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add SVG path parser for straight-line chevron geometry"
```

---

### Task 5: Path normalization

**Files:**
- Create: `src/FlyingAzure.Core/PathGeometry.cs`
- Test: `tests/FlyingAzure.Core.Tests/PathGeometryTests.cs`

**Interfaces:**
- Consumes: `IReadOnlyList<IReadOnlyList<PointF>>` from `SvgPathParser.Parse`.
- Produces: `static class PathGeometry { static IReadOnlyList<IReadOnlyList<PointF>> Normalize(IReadOnlyList<IReadOnlyList<PointF>> subpaths); }` — centers all points on the combined bounding-box center and scales uniformly so the larger bounding-box dimension becomes `1.0`.

- [ ] **Step 1: Write the failing test**

Create `tests/FlyingAzure.Core.Tests/PathGeometryTests.cs`:

```csharp
using System.Drawing;
using FlyingAzure.Core;
using Xunit;

namespace FlyingAzure.Core.Tests;

public class PathGeometryTests
{
    [Fact]
    public void Normalize_SquareZeroToTwo_CentersAndScalesToUnit()
    {
        IReadOnlyList<IReadOnlyList<PointF>> input =
            [[new PointF(0, 0), new PointF(2, 0), new PointF(2, 2), new PointF(0, 2)]];

        var result = PathGeometry.Normalize(input);
        var sub = result[0];

        Assert.Equal(-0.5f, sub[0].X, 3);
        Assert.Equal(-0.5f, sub[0].Y, 3);
        Assert.Equal(0.5f, sub[2].X, 3);
        Assert.Equal(0.5f, sub[2].Y, 3);
    }

    [Fact]
    public void Normalize_WiderThanTall_LargerDimensionBecomesOne()
    {
        IReadOnlyList<IReadOnlyList<PointF>> input =
            [[new PointF(0, 0), new PointF(4, 0), new PointF(4, 2), new PointF(0, 2)]];

        var result = PathGeometry.Normalize(input);
        float minX = result[0].Min(p => p.X), maxX = result[0].Max(p => p.X);
        Assert.Equal(1.0f, maxX - minX, 3);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter PathGeometryTests`
Expected: FAIL — `PathGeometry` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/FlyingAzure.Core/PathGeometry.cs`:

```csharp
using System.Drawing;

namespace FlyingAzure.Core;

public static class PathGeometry
{
    public static IReadOnlyList<IReadOnlyList<PointF>> Normalize(IReadOnlyList<IReadOnlyList<PointF>> subpaths)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var sub in subpaths)
        {
            foreach (var p in sub)
            {
                minX = Math.Min(minX, p.X);
                minY = Math.Min(minY, p.Y);
                maxX = Math.Max(maxX, p.X);
                maxY = Math.Max(maxY, p.Y);
            }
        }

        float cx = (minX + maxX) / 2f;
        float cy = (minY + maxY) / 2f;
        float span = Math.Max(maxX - minX, maxY - minY);
        float scale = span > 0 ? 1f / span : 1f;

        return subpaths
            .Select(sub => (IReadOnlyList<PointF>)sub
                .Select(p => new PointF((p.X - cx) * scale, (p.Y - cy) * scale))
                .ToList())
            .ToList();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter PathGeometryTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add path normalization to unit-centered geometry"
```

---

### Task 6: Sprite simulation

**Files:**
- Create: `src/FlyingAzure.Core/Sprite.cs`
- Create: `src/FlyingAzure.Core/Simulation.cs`
- Test: `tests/FlyingAzure.Core.Tests/SimulationTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `sealed class Sprite { public PointF Position; public float Size; }`
  - `sealed class Simulation` with ctor `(float width, float height, int count, float angleDegrees, float speedPixelsPerSecond, float minSize, float maxSize, Random rng)`, `IReadOnlyList<Sprite> Sprites`, `void Step(double dtSeconds)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/FlyingAzure.Core.Tests/SimulationTests.cs`:

```csharp
using FlyingAzure.Core;
using Xunit;

namespace FlyingAzure.Core.Tests;

public class SimulationTests
{
    private static Simulation Make(int count, float angle) =>
        new(800, 600, count, angle, speedPixelsPerSecond: 100, minSize: 40, maxSize: 40, rng: new Random(1));

    [Fact]
    public void Ctor_PopulatesRequestedCount()
    {
        var sim = Make(15, 150);
        Assert.Equal(15, sim.Sprites.Count);
    }

    [Fact]
    public void Step_MovesSpriteAlongAngle()
    {
        var sim = Make(1, 0); // angle 0 => dx=1, dy=0
        var start = sim.Sprites[0].Position;
        sim.Step(1.0); // 100 px/sec * 1 sec = +100 x
        Assert.Equal(start.X + 100f, sim.Sprites[0].Position.X, 2);
        Assert.Equal(start.Y, sim.Sprites[0].Position.Y, 2);
    }

    [Fact]
    public void Step_RespawnsAfterExitingLeft_WhenMovingLeft()
    {
        var sim = Make(1, 180); // dx=-1, dy=0 => exits left, re-enters right
        sim.Sprites[0].Position = new System.Drawing.PointF(-5000, 300);
        sim.Step(0.001);
        Assert.True(sim.Sprites[0].Position.X > 800, $"expected re-entry from right, got X={sim.Sprites[0].Position.X}");
    }

    [Fact]
    public void Step_RespawnsFromTop_WhenMovingDown()
    {
        var sim = Make(1, 90); // dy≈1 => exits bottom, re-enters top
        sim.Sprites[0].Position = new System.Drawing.PointF(400, 50000);
        sim.Step(0.001);
        Assert.True(sim.Sprites[0].Position.Y < 0, $"expected re-entry from top, got Y={sim.Sprites[0].Position.Y}");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter SimulationTests`
Expected: FAIL — `Simulation` / `Sprite` do not exist.

- [ ] **Step 3: Write `Sprite.cs`**

Create `src/FlyingAzure.Core/Sprite.cs`:

```csharp
using System.Drawing;

namespace FlyingAzure.Core;

public sealed class Sprite
{
    public PointF Position;
    public float Size;
}
```

- [ ] **Step 4: Write `Simulation.cs`**

Create `src/FlyingAzure.Core/Simulation.cs`:

```csharp
using System.Drawing;

namespace FlyingAzure.Core;

public sealed class Simulation
{
    private readonly Random _rng;
    private readonly List<Sprite> _sprites = [];

    public float Width { get; }
    public float Height { get; }
    public float AngleDegrees { get; }
    public float SpeedPixelsPerSecond { get; }
    public float MinSize { get; }
    public float MaxSize { get; }

    public IReadOnlyList<Sprite> Sprites => _sprites;

    public Simulation(float width, float height, int count, float angleDegrees,
        float speedPixelsPerSecond, float minSize, float maxSize, Random rng)
    {
        Width = width;
        Height = height;
        AngleDegrees = angleDegrees;
        SpeedPixelsPerSecond = speedPixelsPerSecond;
        MinSize = minSize;
        MaxSize = maxSize;
        _rng = rng;

        for (int n = 0; n < count; n++)
        {
            _sprites.Add(new Sprite
            {
                Position = new PointF((float)(_rng.NextDouble() * width), (float)(_rng.NextDouble() * height)),
                Size = RandomSize(),
            });
        }
    }

    public void Step(double dtSeconds)
    {
        var (dx, dy) = Direction();
        float step = SpeedPixelsPerSecond * (float)dtSeconds;

        foreach (var s in _sprites)
        {
            s.Position = new PointF(s.Position.X + dx * step, s.Position.Y + dy * step);
            float margin = s.Size;
            if (s.Position.X < -margin || s.Position.X > Width + margin ||
                s.Position.Y < -margin || s.Position.Y > Height + margin)
            {
                Respawn(s);
            }
        }
    }

    private (float dx, float dy) Direction()
    {
        float radians = AngleDegrees * MathF.PI / 180f;
        return (MathF.Cos(radians), MathF.Sin(radians));
    }

    private float RandomSize() => MinSize + (float)_rng.NextDouble() * (MaxSize - MinSize);

    private void Respawn(Sprite s)
    {
        var (dx, dy) = Direction();
        float adx = MathF.Abs(dx), ady = MathF.Abs(dy);
        s.Size = RandomSize();
        float margin = s.Size;

        // Choose horizontal vs vertical entry edge proportional to the direction components.
        if (_rng.NextDouble() * (adx + ady) < adx)
        {
            s.Position = new PointF(dx < 0 ? Width + margin : -margin, (float)(_rng.NextDouble() * Height));
        }
        else
        {
            s.Position = new PointF((float)(_rng.NextDouble() * Width), dy > 0 ? -margin : Height + margin);
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter SimulationTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add sprite simulation with directional respawn"
```

---

### Task 7: Chevron renderer

**Files:**
- Create: `src/FlyingAzure/ChevronRenderer.cs`
- Modify: `src/FlyingAzure/FlyingAzure.csproj` (embed the asset)
- Test: `tests/FlyingAzure.Core.Tests/ChevronRendererTests.cs`

**Interfaces:**
- Consumes: `SvgPathParser.Parse`, `PathGeometry.Normalize`, `Sprite`.
- Produces: `sealed class ChevronRenderer : IDisposable` with `ChevronRenderer(string svgPathData)`, `static ChevronRenderer FromEmbeddedAsset()`, `GraphicsPath Path { get; }`, `void Draw(Graphics g, Sprite sprite)`. Logo is filled `#00ABEC`, upright, scaled by `sprite.Size`, centered on `sprite.Position`.

- [ ] **Step 1: Embed the chevron asset**

Add to `src/FlyingAzure/FlyingAzure.csproj` inside an `<ItemGroup>`:

```xml
  <ItemGroup>
    <EmbeddedResource Include="..\..\assets\azure-chevron.svg" Link="assets\azure-chevron.svg">
      <LogicalName>azure-chevron.svg</LogicalName>
    </EmbeddedResource>
  </ItemGroup>
```

- [ ] **Step 2: Write the failing test**

Create `tests/FlyingAzure.Core.Tests/ChevronRendererTests.cs`:

```csharp
using Xunit;

namespace FlyingAzure.Core.Tests;

public class ChevronRendererTests
{
    [Fact]
    public void FromEmbeddedAsset_BuildsUnitWidthPath()
    {
        using var renderer = FlyingAzure.ChevronRenderer.FromEmbeddedAsset();
        var bounds = renderer.Path.GetBounds();
        // The chevron is 16 wide (the larger dimension), so normalized width ≈ 1.0.
        Assert.InRange(bounds.Width, 0.98f, 1.02f);
        Assert.InRange(bounds.Height, 0.6f, 0.9f);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test --filter ChevronRendererTests`
Expected: FAIL — `ChevronRenderer` does not exist.

- [ ] **Step 4: Write the implementation**

Create `src/FlyingAzure/ChevronRenderer.cs`:

```csharp
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using FlyingAzure.Core;

namespace FlyingAzure;

public sealed partial class ChevronRenderer : IDisposable
{
    private static readonly Color AzureBlue = Color.FromArgb(0x00, 0xAB, 0xEC);

    private readonly GraphicsPath _path;
    private readonly SolidBrush _brush = new(AzureBlue);

    public ChevronRenderer(string svgPathData)
    {
        var normalized = PathGeometry.Normalize(SvgPathParser.Parse(svgPathData));
        _path = new GraphicsPath { FillMode = FillMode.Winding };
        foreach (var sub in normalized)
        {
            _path.AddPolygon(sub.ToArray());
        }
    }

    public GraphicsPath Path => _path;

    public void Draw(Graphics g, Sprite sprite)
    {
        var state = g.Save();
        g.TranslateTransform(sprite.Position.X, sprite.Position.Y);
        g.ScaleTransform(sprite.Size, sprite.Size);
        g.FillPath(_brush, _path);
        g.Restore(state);
    }

    public static ChevronRenderer FromEmbeddedAsset()
    {
        using var stream = typeof(ChevronRenderer).Assembly.GetManifestResourceStream("azure-chevron.svg")
            ?? throw new InvalidOperationException("Embedded resource 'azure-chevron.svg' was not found.");
        using var reader = new StreamReader(stream);
        string svg = reader.ReadToEnd();

        var match = PathDataRegex().Match(svg);
        if (!match.Success)
        {
            throw new FormatException("No path data found in chevron SVG.");
        }

        return new ChevronRenderer(match.Groups[1].Value);
    }

    [GeneratedRegex("d=\"([^\"]+)\"")]
    private static partial Regex PathDataRegex();

    public void Dispose()
    {
        _path.Dispose();
        _brush.Dispose();
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter ChevronRendererTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add chevron renderer with embedded Azure logo asset"
```

---

### Task 8: Animated surface, screensaver form, and run-mode dispatch

**Files:**
- Create: `src/FlyingAzure/AnimatedSurface.cs`
- Create: `src/FlyingAzure/ScreensaverForm.cs`
- Modify: `src/FlyingAzure/Program.cs`

**Interfaces:**
- Consumes: `Settings`, `Simulation`, `ChevronRenderer`, `CommandLineParser`, `SettingsStore`, `RegistrySettingsBackend`.
- Produces:
  - `sealed class AnimatedSurface : Control` with ctor `(Settings settings, ChevronRenderer renderer)` and `void ApplySettings(Settings settings)`.
  - `sealed class ScreensaverForm : Form` with ctor `(Settings settings, Rectangle bounds, ChevronRenderer renderer)` and `event EventHandler? ExitRequested`.
  - `Program.Main` dispatching Run/Configure/Preview. Configure and Preview are stubbed here (real forms in Tasks 9–10) so this task is independently runnable for `/s`.

- [ ] **Step 1: Write `AnimatedSurface.cs`**

This is the shared render loop: an accumulation buffer, a ~60 FPS timer, the per-frame fade rectangle, and sprite drawing. Create `src/FlyingAzure/AnimatedSurface.cs`:

```csharp
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using FlyingAzure.Core;

namespace FlyingAzure;

public sealed class AnimatedSurface : Control
{
    private const float TravelAngleDegrees = 150f; // down-left

    private readonly ChevronRenderer _renderer;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Random _rng = new();

    private Settings _settings;
    private Simulation? _sim;
    private Bitmap? _buffer;
    private long _lastMs;

    public AnimatedSurface(Settings settings, ChevronRenderer renderer)
    {
        _settings = settings;
        _renderer = renderer;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque | ControlStyles.UserPaint, true);
        _timer = new System.Windows.Forms.Timer { Interval = 16 };
        _timer.Tick += (_, _) => Tick();
    }

    public void ApplySettings(Settings settings)
    {
        _settings = settings;
        Rebuild();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Rebuild();
        _timer.Start();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Rebuild();
    }

    private void Rebuild()
    {
        if (!IsHandleCreated || Width <= 0 || Height <= 0)
        {
            return;
        }

        _buffer?.Dispose();
        _buffer = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(_buffer))
        {
            g.Clear(_settings.BackgroundColor());
        }

        float baseSize = _settings.BaseSizePixels();
        _sim = new Simulation(Width, Height, _settings.LogoCount, TravelAngleDegrees,
            _settings.SpeedPixelsPerSecond(), baseSize * 0.7f, baseSize * 1.3f, _rng);
        _lastMs = _clock.ElapsedMilliseconds;
    }

    private void Tick()
    {
        if (_sim is null || _buffer is null)
        {
            return;
        }

        long now = _clock.ElapsedMilliseconds;
        double dt = Math.Min((now - _lastMs) / 1000.0, 0.1);
        _lastMs = now;
        _sim.Step(dt);

        using (var g = Graphics.FromImage(_buffer))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var fade = new SolidBrush(Color.FromArgb(_settings.FadeAlpha(), _settings.BackgroundColor()));
            g.FillRectangle(fade, 0, 0, _buffer.Width, _buffer.Height);
            foreach (var sprite in _sim.Sprites)
            {
                _renderer.Draw(g, sprite);
            }
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_buffer is not null)
        {
            e.Graphics.DrawImageUnscaled(_buffer, 0, 0);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // Intentionally empty: the buffer owns every pixel.
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _buffer?.Dispose();
        }

        base.Dispose(disposing);
    }
}
```

- [ ] **Step 2: Write `ScreensaverForm.cs`**

Create `src/FlyingAzure/ScreensaverForm.cs`:

```csharp
using System.Drawing;
using FlyingAzure.Core;

namespace FlyingAzure;

public sealed class ScreensaverForm : Form
{
    private const int MouseDeadZonePixels = 8;

    private Point _initialMouse;
    private bool _mouseInitialized;
    private bool _exiting;

    public event EventHandler? ExitRequested;

    public ScreensaverForm(Settings settings, Rectangle bounds, ChevronRenderer renderer)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = settings.BackgroundColor();
        KeyPreview = true;
        DoubleBuffered = true;

        var surface = new AnimatedSurface(settings, renderer) { Dock = DockStyle.Fill };
        surface.MouseMove += (_, _) => OnUserMouseMove();
        surface.MouseDown += (_, _) => RequestExit();
        Controls.Add(surface);

        Cursor.Hide();
    }

    protected override void OnKeyDown(KeyEventArgs e) => RequestExit();

    protected override void OnMouseDown(MouseEventArgs e) => RequestExit();

    protected override void OnMouseMove(MouseEventArgs e) => OnUserMouseMove();

    private void OnUserMouseMove()
    {
        var pos = Cursor.Position;
        if (!_mouseInitialized)
        {
            _initialMouse = pos;
            _mouseInitialized = true;
            return;
        }

        if (Math.Abs(pos.X - _initialMouse.X) > MouseDeadZonePixels ||
            Math.Abs(pos.Y - _initialMouse.Y) > MouseDeadZonePixels)
        {
            RequestExit();
        }
    }

    private void RequestExit()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        Cursor.Show();
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Cursor.Show();
        }

        base.Dispose(disposing);
    }
}
```

- [ ] **Step 3: Replace `Program.cs` with full dispatch**

Overwrite `src/FlyingAzure/Program.cs`:

```csharp
using FlyingAzure.Core;

namespace FlyingAzure;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetCompatibleTextRenderingDefault(false);

        var parsed = CommandLineParser.Parse(args);
        var settings = new SettingsStore(new RegistrySettingsBackend()).Load();

        switch (parsed.Mode)
        {
            case ScreensaverMode.Preview:
                // Implemented in Task 9.
                return 0;
            case ScreensaverMode.Configure:
                // Implemented in Task 10.
                return 0;
            case ScreensaverMode.Run:
            default:
                RunScreensaver(settings);
                return 0;
        }
    }

    private static void RunScreensaver(Settings settings)
    {
        using var renderer = ChevronRenderer.FromEmbeddedAsset();
        var forms = new List<ScreensaverForm>();

        foreach (var screen in Screen.AllScreens)
        {
            var form = new ScreensaverForm(settings, screen.Bounds, renderer);
            form.ExitRequested += (_, _) => CloseAll(forms);
            forms.Add(form);
        }

        foreach (var form in forms)
        {
            form.Show();
        }

        Application.Run();
    }

    private static void CloseAll(List<ScreensaverForm> forms)
    {
        foreach (var form in forms)
        {
            if (!form.IsDisposed)
            {
                form.Close();
            }
        }

        Application.Exit();
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Manually verify run mode**

Run: `dotnet run --project src/FlyingAzure -- /s`
Expected: Full-screen black window(s), Azure chevrons drifting down-left with fading trails on every monitor. Pressing a key or moving the mouse closes all windows and the process exits 0.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add animated surface, screensaver form, and run-mode dispatch"
```

---

### Task 9: Preview mode (`/p <hwnd>`)

**Files:**
- Create: `src/FlyingAzure/PreviewHost.cs`
- Modify: `src/FlyingAzure/Program.cs:Main` (Preview case)

**Interfaces:**
- Consumes: `Settings`, `ChevronRenderer`, `AnimatedSurface`.
- Produces: `static class PreviewHost { static void Run(Settings settings, nint parentHandle); }` — hosts an `AnimatedSurface` inside the supplied preview window and exits when that window is destroyed.

- [ ] **Step 1: Write `PreviewHost.cs`**

Create `src/FlyingAzure/PreviewHost.cs`:

```csharp
using System.Drawing;
using System.Runtime.InteropServices;
using FlyingAzure.Core;

namespace FlyingAzure;

public static partial class PreviewHost
{
    private const int GWL_STYLE = -16;
    private const long WS_CHILD = 0x40000000L;
    private const long WS_POPUP = 0x80000000L;

    public static void Run(Settings settings, nint parentHandle)
    {
        if (parentHandle == 0 || !IsWindow(parentHandle))
        {
            return;
        }

        using var renderer = ChevronRenderer.FromEmbeddedAsset();
        GetClientRect(parentHandle, out RECT rc);

        var form = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            BackColor = settings.BackgroundColor(),
        };
        var surface = new AnimatedSurface(settings, renderer) { Dock = DockStyle.Fill };
        form.Controls.Add(surface);

        form.Load += (_, _) =>
        {
            long style = GetWindowLongPtr(form.Handle, GWL_STYLE).ToInt64();
            style = (style | WS_CHILD) & ~WS_POPUP;
            SetWindowLongPtr(form.Handle, GWL_STYLE, (nint)style);
            SetParent(form.Handle, parentHandle);
            form.Location = Point.Empty;
            form.Size = new Size(rc.Right - rc.Left, rc.Bottom - rc.Top);
        };

        var watchdog = new System.Windows.Forms.Timer { Interval = 500 };
        watchdog.Tick += (_, _) =>
        {
            if (!IsWindow(parentHandle))
            {
                Application.Exit();
            }
        };
        watchdog.Start();

        Application.Run(form);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [LibraryImport("user32.dll")]
    private static partial nint SetParent(nint child, nint parent);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(nint hWnd, out RECT rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindow(nint hWnd);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(nint hWnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr(nint hWnd, int index, nint value);
}
```

- [ ] **Step 2: Wire the Preview case in `Program.Main`**

In `src/FlyingAzure/Program.cs`, replace the `case ScreensaverMode.Preview:` body:

```csharp
            case ScreensaverMode.Preview:
                PreviewHost.Run(settings, parsed.WindowHandle);
                return 0;
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Manually verify preview**

Build Release and install (see Task 11), then open the Windows Screen Saver Settings dialog and select "FlyingAzure" — the small monitor preview should show animated chevrons. (Before Task 11 is done, this step can be deferred; note it as pending.)

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add screensaver preview hosting for /p mode"
```

---

### Task 10: Configuration dialog (`/c`)

**Files:**
- Create: `src/FlyingAzure/ConfigForm.cs`
- Modify: `src/FlyingAzure/Program.cs:Main` (Configure case)

**Interfaces:**
- Consumes: `Settings`, `SettingsStore`, `RegistrySettingsBackend`, `ChevronRenderer`, `AnimatedSurface`.
- Produces: `sealed class ConfigForm : Form` with ctor `(Settings settings, SettingsStore store, ChevronRenderer renderer)`. Standard-set controls (count, speed, size, trail, background color) with a live preview; OK saves to the store, Cancel discards.

- [ ] **Step 1: Write `ConfigForm.cs`**

Create `src/FlyingAzure/ConfigForm.cs`:

```csharp
using System.Drawing;
using FlyingAzure.Core;

namespace FlyingAzure;

public sealed class ConfigForm : Form
{
    private readonly SettingsStore _store;
    private readonly AnimatedSurface _preview;

    private readonly TrackBar _count = NewTrack(1, 80);
    private readonly TrackBar _speed = NewTrack(0, 100);
    private readonly TrackBar _size = NewTrack(0, 100);
    private readonly TrackBar _trail = NewTrack(0, 100);
    private readonly Button _colorButton = new() { Text = "Background…", Width = 110, Height = 28 };

    private Color _background;

    public ConfigForm(Settings settings, SettingsStore store, ChevronRenderer renderer)
    {
        _store = store;
        _background = settings.BackgroundColor();

        Text = "Flying Azure — Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(440, 360);

        _count.Value = settings.LogoCount;
        _speed.Value = settings.Speed;
        _size.Value = settings.Size;
        _trail.Value = settings.TrailLength;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            Padding = new Padding(12),
        };
        AddRow(layout, "Number of logos", _count);
        AddRow(layout, "Speed", _speed);
        AddRow(layout, "Logo size", _size);
        AddRow(layout, "Trail length", _trail);
        AddRow(layout, "Background", _colorButton);

        _preview = new AnimatedSurface(CurrentSettings(), renderer)
        {
            Height = 120,
            Dock = DockStyle.Fill,
            BackColor = _background,
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 44,
            Padding = new Padding(8),
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        Controls.Add(_preview);
        Controls.Add(buttons);
        Controls.Add(layout);

        foreach (var track in new[] { _count, _speed, _size, _trail })
        {
            track.ValueChanged += (_, _) => RefreshPreview();
        }

        _colorButton.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = _background, FullOpen = true };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _background = dialog.Color;
                RefreshPreview();
            }
        };

        ok.Click += (_, _) => _store.Save(CurrentSettings());
    }

    private Settings CurrentSettings() => new()
    {
        LogoCount = _count.Value,
        Speed = _speed.Value,
        Size = _size.Value,
        TrailLength = _trail.Value,
        BackgroundArgb = _background.ToArgb(),
    };

    private void RefreshPreview()
    {
        _preview.BackColor = _background;
        _preview.ApplySettings(CurrentSettings());
    }

    private static TrackBar NewTrack(int min, int max) => new()
    {
        Minimum = min,
        Maximum = max,
        TickStyle = TickStyle.None,
        Width = 240,
    };

    private static void AddRow(TableLayoutPanel layout, string label, Control control)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) });
        layout.Controls.Add(control);
    }
}
```

- [ ] **Step 2: Wire the Configure case in `Program.Main`**

In `src/FlyingAzure/Program.cs`, replace the `case ScreensaverMode.Configure:` body:

```csharp
            case ScreensaverMode.Configure:
            {
                using var renderer = ChevronRenderer.FromEmbeddedAsset();
                var store = new SettingsStore(new RegistrySettingsBackend());
                using var form = new ConfigForm(settings, store, renderer);
                Application.Run(form);
                return 0;
            }
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Manually verify config**

Run: `dotnet run --project src/FlyingAzure -- /c`
Expected: A settings dialog with four sliders, a background-color button, and a live animated preview. Changing sliders updates the preview. OK persists to `HKCU\Software\FlyingAzure`; reopening shows the saved values. Cancel discards.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add configuration dialog with live preview"
```

---

### Task 11: Packaging to `.scr`, README, and final verification

**Files:**
- Modify: `src/FlyingAzure/FlyingAzure.csproj` (assembly name + post-build `.scr` copy)
- Create: `README.md`

**Interfaces:**
- Consumes: the built `FlyingAzure` executable.
- Produces: a `FlyingAzure.scr` alongside the build output and install/build documentation.

- [ ] **Step 1: Set assembly name and add the `.scr` copy target**

In `src/FlyingAzure/FlyingAzure.csproj`, ensure `<AssemblyName>FlyingAzure</AssemblyName>` in the main `<PropertyGroup>`, then add:

```xml
  <Target Name="CopyToScr" AfterTargets="Build">
    <Copy SourceFiles="$(TargetPath)" DestinationFiles="$(TargetDir)FlyingAzure.scr" />
  </Target>
```

- [ ] **Step 2: Write `README.md`**

Create `README.md` at repo root:

```markdown
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
```

- [ ] **Step 3: Build Release and confirm the `.scr` exists**

Run:
```bash
dotnet build -c Release
ls src/FlyingAzure/bin/Release/net10.0-windows/FlyingAzure.scr
```
Expected: the `.scr` file is listed.

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test`
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "build: package as .scr and add README"
```

---

## Self-Review Notes

- **Spec coverage:** process contract (Task 2, 8, 9, 10), multi-monitor run (Task 8), fade-trail (Task 8 `AnimatedSurface.Tick`), Azure chevron asset (Task 7), standard-set config + registry (Tasks 3, 10), testable Core (Tasks 2–6), `.scr` packaging (Task 11). The spec's "upright logos" and "down-left default angle" are encoded as the Global Constraints and in `AnimatedSurface.TravelAngleDegrees`.
- **Spec correction:** the spec text floated "~210°"; that heads up-left in screen coordinates. The plan uses `150f` (down-left), matching the Flying Toasters flow and the "across at an angle" brief. Recorded here intentionally.
- **Type consistency:** `Settings` property names, `Simulation` ctor parameter order, and `ChevronRenderer.Draw(Graphics, Sprite)` signature are used identically across Tasks 6–10.
```
