using FlyingAzure.Core;
using SkiaSharp;

namespace FlyingAzure.Engine;

/// <summary>
/// The shared, platform-agnostic render engine: owns a <see cref="Simulation"/>, the chevron
/// sprite cache, and the clock overlay, and renders one full frame into an <see cref="SKCanvas"/>.
/// Consumed two ways — directly by the test suite, and via <see cref="EngineExports"/>'s C ABI by
/// the native hosts (Windows .scr / macOS .saver). One instance covers one view (no cross-monitor
/// offset; each macOS screen view runs its own instance sized to that screen).
/// </summary>
public sealed class EngineRenderer : IDisposable
{
    private const float TravelAngleDegrees = 150f; // down-left — matches the Windows host
    private const float GhostSpacingFactor = 0.18f; // gap between ghosts, relative to logo size

    private readonly int _width;
    private readonly int _height;
    private readonly ClockCorner _clockCorner;
    private readonly SKColor _background;
    private readonly SKPath _unitPath;
    private readonly Simulation _simulation;
    private readonly SkiaSpriteCache _cache;
    private readonly SkiaClockOverlay _clock;
    private readonly float _dirX;
    private readonly float _dirY;

    public EngineRenderer(int width, int height, Settings settings, Random rng)
    {
        _width = width;
        _height = height;
        var s = settings.Clamp();
        _clockCorner = s.Clock;

        var bg = s.BackgroundColor();
        _background = new SKColor(bg.R, bg.G, bg.B, 255);

        _unitPath = SkiaChevron.BuildUnitPath(SkiaChevron.LoadEmbeddedSvgPathData());
        _simulation = new Simulation(width, height, s.LogoCount, TravelAngleDegrees,
            s.SpeedPixelsPerSecond(), s.MinLogoSizePixels(), s.MaxLogoSizePixels(), rng);
        (_dirX, _dirY) = _simulation.Direction();
        _cache = new SkiaSpriteCache(_unitPath, SkiaChevron.AzureBlue,
            s.MinLogoSizePixels(), s.MaxLogoSizePixels(), s.GhostCount());
        _clock = new SkiaClockOverlay(height);
    }

    public void Step(double dtSeconds) => _simulation.Step(dtSeconds);

    public void Render(SKCanvas canvas)
    {
        canvas.Clear(_background);

        int ghostCount = _cache.GhostCount;
        foreach (var sprite in _simulation.Sprites)
        {
            float x = sprite.Position.X;
            float y = sprite.Position.Y;
            float margin = sprite.Size;
            float gap = sprite.Size * GhostSpacingFactor;
            int bucket = _cache.BucketOf(sprite.Size);

            for (int k = ghostCount; k >= 1; k--)
            {
                float gx = x - _dirX * gap * k;
                float gy = y - _dirY * gap * k;
                if (gx < -margin || gx > _width + margin || gy < -margin || gy > _height + margin)
                {
                    continue;
                }

                _cache.Draw(canvas, gx, gy, bucket, k);
            }

            if (x >= -margin && x <= _width + margin && y >= -margin && y <= _height + margin)
            {
                _cache.Draw(canvas, x, y, bucket, 0);
            }
        }

        _clock.Draw(canvas, _width, _height, _clockCorner);
    }

    public void Dispose()
    {
        _clock.Dispose();
        _cache.Dispose();
        _unitPath.Dispose();
    }
}
