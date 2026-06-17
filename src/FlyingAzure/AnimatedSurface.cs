using System.Diagnostics;
using System.Drawing;
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
    private SpriteCache? _cache;
    private ClockOverlay? _clockOverlay;
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

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _timer.Stop();
        base.OnHandleDestroyed(e);
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
        _buffer = new Bitmap(Width, Height, PixelFormat.Format32bppRgb);
        using (var g = Graphics.FromImage(_buffer))
        {
            g.Clear(_settings.BackgroundColor());
        }

        _sim = new Simulation(Width, Height, _settings.LogoCount, TravelAngleDegrees,
            _settings.SpeedPixelsPerSecond(), _settings.MinLogoSizePixels(), _settings.MaxLogoSizePixels(), _rng);

        _cache?.Dispose();
        _cache = _renderer.CreateSpriteCache(_settings.MinLogoSizePixels(), _settings.MaxLogoSizePixels(), _settings.GhostCount());

        _clockOverlay?.Dispose();
        _clockOverlay = new ClockOverlay(Height);
        _lastMs = _clock.ElapsedMilliseconds;
    }

    private void Tick()
    {
        if (_sim is null || _buffer is null || _cache is null)
        {
            return;
        }

        long now = _clock.ElapsedMilliseconds;
        double dt = Math.Min((now - _lastMs) / 1000.0, 0.1);
        _lastMs = now;
        _sim.Step(dt);

        using (var g = Graphics.FromImage(_buffer))
        {
            var (dirX, dirY) = _sim.Direction();
            TrailRenderer.Render(g, _buffer.Width, _buffer.Height, _settings.BackgroundColor(),
                _sim.Sprites, 0f, 0f, dirX, dirY, _cache);
            _clockOverlay?.Draw(g, _buffer.Width, _buffer.Height, _settings.Clock);
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
            _cache?.Dispose();
            _clockOverlay?.Dispose();
        }

        base.Dispose(disposing);
    }
}
