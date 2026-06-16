using System.Drawing;
using FlyingAzure.Core;
using Xunit;

namespace FlyingAzure.Core.Tests;

public class SvgPathParserTests
{
    private static void AssertPoint(PointF expected, PointF actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
    }

    [Fact]
    public void Parse_AbsoluteSquare_ReturnsFourPoints()
    {
        var result = SvgPathParser.Parse("M0 0 L2 0 L2 2 L0 2 Z");
        var sub = Assert.Single(result);
        Assert.Equal(4, sub.Count);
        AssertPoint(new PointF(0, 0), sub[0]);
        AssertPoint(new PointF(2, 0), sub[1]);
        AssertPoint(new PointF(2, 2), sub[2]);
        AssertPoint(new PointF(0, 2), sub[3]);
    }

    [Fact]
    public void Parse_HandlesHVAndRelative()
    {
        // M then H (abs horiz), then relative v
        var result = SvgPathParser.Parse("M1 1 H5 v3");
        var sub = Assert.Single(result);
        AssertPoint(new PointF(1, 1), sub[0]);
        AssertPoint(new PointF(5, 1), sub[1]);
        AssertPoint(new PointF(5, 4), sub[2]);
    }

    [Fact]
    public void Parse_Chevron_ProducesTwoSubpaths()
    {
        const string d = "M3.65 14.2H16L9.35 2.68 7.33 8.24l3.88 4.63-7.56 1.33zM8.82 1.8L4.07 5.79 0 12.84h3.67v.01L8.82 1.8z";
        var result = SvgPathParser.Parse(d);
        Assert.Equal(2, result.Count);
        AssertPoint(new PointF(3.65f, 14.2f), result[0][0]);
        AssertPoint(new PointF(16f, 14.2f), result[0][1]);
        AssertPoint(new PointF(9.35f, 2.68f), result[0][2]);
        AssertPoint(new PointF(7.33f, 8.24f), result[0][3]);
        AssertPoint(new PointF(11.21f, 12.87f), result[0][4]);
        AssertPoint(new PointF(3.65f, 14.2f), result[0][5]);
        AssertPoint(new PointF(8.82f, 1.8f), result[1][0]);
        AssertPoint(new PointF(0f, 12.84f), result[1][2]);
        AssertPoint(new PointF(3.67f, 12.84f), result[1][3]);
        AssertPoint(new PointF(3.67f, 12.85f), result[1][4]);
    }

    [Fact]
    public void Parse_UnsupportedCommand_Throws() =>
        Assert.Throws<FormatException>(() => SvgPathParser.Parse("M0 0 C1 1 2 2 3 3"));
}
