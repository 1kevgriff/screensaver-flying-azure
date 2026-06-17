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
    public void Step_MovesSpriteAtItsOwnSpeedAlongAngle()
    {
        var sim = Make(1, 0); // angle 0 => dx=1, dy=0
        var sprite = sim.Sprites[0];
        var start = sprite.Position;
        sim.Step(1.0); // moves the sprite's own per-sprite speed in 1 second
        Assert.Equal(start.X + sprite.Speed, sprite.Position.X, 2);
        Assert.Equal(start.Y, sprite.Position.Y, 2);
    }

    [Fact]
    public void Ctor_GivesSpritesVariedSpeedsAroundTheBase()
    {
        var sim = Make(40, 150); // base speed 100
        var speeds = sim.Sprites.Select(s => s.Speed).ToList();

        // Each sprite stays within +/-60% of the base (100), but they are not all identical.
        Assert.All(speeds, sp => Assert.InRange(sp, 40f, 160f));
        Assert.True(speeds.Distinct().Count() > 1, "expected logos to fly at different speeds");
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
