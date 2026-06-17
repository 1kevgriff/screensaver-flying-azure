using System.Drawing;

namespace FlyingAzure.Core;

public sealed class Sprite
{
    public PointF Position;
    public float Size;

    /// <summary>This logo's own travel speed in pixels/second. Varies per sprite (around the
    /// simulation's base speed) so some logos fly faster than others.</summary>
    public float Speed;
}
