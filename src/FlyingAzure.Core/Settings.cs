using System.Drawing;

namespace FlyingAzure.Core;

public sealed record Settings
{
    public int LogoCount { get; init; } = 24;
    public int Speed { get; init; } = 50;        // 0..100
    public int Size { get; init; } = 50;          // 0..100
    public int TrailLength { get; init; } = 55;   // 0..100 (higher = longer trail)
    public int BackgroundArgb { get; init; } = unchecked((int)0xFF000000); // opaque black
    public ClockCorner Clock { get; init; } = ClockCorner.BottomRight;

    public static Settings Default => new();

    public Settings Clamp() => this with
    {
        LogoCount = Math.Clamp(LogoCount, 1, 80),
        Speed = Math.Clamp(Speed, 0, 100),
        Size = Math.Clamp(Size, 0, 100),
        TrailLength = Math.Clamp(TrailLength, 0, 100),
        Clock = Enum.IsDefined(Clock) ? Clock : ClockCorner.BottomRight,
    };

    public int SpeedPixelsPerSecond() => 30 + (int)Math.Round(Math.Clamp(Speed, 0, 100) / 100.0 * 270);

    public float BaseSizePixels() => 28f + Math.Clamp(Size, 0, 100) / 100f * 172f;

    // Per-logo size varies +/-30% around the base for depth.
    public float MinLogoSizePixels() => BaseSizePixels() * 0.7f;

    public float MaxLogoSizePixels() => BaseSizePixels() * 1.3f;

    /// <summary>
    /// Number of fading "ghost" copies drawn behind each logo to form its motion trail.
    /// Longer trail => more ghosts. Resolution-independent (cost scales with sprites, not pixels).
    /// </summary>
    public int GhostCount() => (int)Math.Round(Math.Clamp(TrailLength, 0, 100) / 100.0 * 16);

    public Color BackgroundColor() => Color.FromArgb(BackgroundArgb);
}
