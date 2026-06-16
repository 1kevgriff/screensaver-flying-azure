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
