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
