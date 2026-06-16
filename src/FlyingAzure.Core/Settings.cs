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
