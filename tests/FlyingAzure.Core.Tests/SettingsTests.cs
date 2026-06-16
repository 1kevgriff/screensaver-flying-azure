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
