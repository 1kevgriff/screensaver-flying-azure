using System.Drawing;

namespace FlyingAzure.Core;

public sealed class Simulation
{
    // Each logo flies at the base speed scaled by a random factor in [1 - V, 1 + V], so some
    // are noticeably faster than others while the field's average speed tracks the Speed setting.
    // 0.6 => fastest logos fly ~4x the slowest, enough for the spread to read at a glance.
    private const float SpeedVariation = 0.6f;

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
                Speed = RandomSpeed(),
            });
        }
    }

    public void Step(double dtSeconds)
    {
        var (dx, dy) = Direction();

        foreach (var s in _sprites)
        {
            float step = s.Speed * (float)dtSeconds;
            s.Position = new PointF(s.Position.X + dx * step, s.Position.Y + dy * step);
            float margin = s.Size;
            if (s.Position.X < -margin || s.Position.X > Width + margin ||
                s.Position.Y < -margin || s.Position.Y > Height + margin)
            {
                Respawn(s);
            }
        }
    }

    /// <summary>Unit travel vector (cos, sin of <see cref="AngleDegrees"/>) — the single
    /// source of truth callers use for trail/ghost direction instead of recomputing it.</summary>
    public (float X, float Y) Direction()
    {
        float radians = AngleDegrees * MathF.PI / 180f;
        return (MathF.Cos(radians), MathF.Sin(radians));
    }

    private float RandomSize() => MinSize + (float)_rng.NextDouble() * (MaxSize - MinSize);

    private float RandomSpeed() =>
        SpeedPixelsPerSecond * (1f - SpeedVariation + (float)_rng.NextDouble() * 2f * SpeedVariation);

    private void Respawn(Sprite s)
    {
        var (dx, dy) = Direction();
        float adx = MathF.Abs(dx), ady = MathF.Abs(dy);
        s.Size = RandomSize();
        s.Speed = RandomSpeed();
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
