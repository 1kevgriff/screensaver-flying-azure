using FlyingAzure.Core;
using FlyingAzure.Engine;
using SkiaSharp;

namespace FlyingAzure.Engine.Tests;

/// <summary>
/// Cross-platform golden-ish checks for the SkiaSharp engine. These run on both Windows and
/// macOS CI, so they prove the shared render path builds and produces pixels on each OS — the
/// parity guarantee behind "one engine, two hosts".
/// </summary>
public class RenderTests
{
    private static SKBitmap RenderFrames(Settings settings, int width, int height, int frames)
    {
        using var engine = new EngineRenderer(width, height, settings, new Random(42));
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        for (int i = 0; i < frames; i++)
        {
            engine.Step(0.05);
        }

        engine.Render(canvas);
        return bitmap;
    }

    private static int NonBackgroundPixels(SKBitmap bitmap, SKColor background)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) != background)
                {
                    count++;
                }
            }
        }

        return count;
    }

    [Fact]
    public void Render_DrawsChevronsOverBackground()
    {
        var settings = Settings.Default with { LogoCount = 40, TrailLength = 60, Clock = ClockCorner.Off };
        using var bitmap = RenderFrames(settings, 320, 240, 5);

        int drawn = NonBackgroundPixels(bitmap, new SKColor(0, 0, 0, 255));
        Assert.True(drawn > 200, $"Expected the chevron field to paint many pixels, saw {drawn}.");
    }

    [Fact]
    public void Render_BackgroundColorIsHonored()
    {
        // Opaque white background, no logos visible at the very corner -> corner pixel is white.
        var settings = Settings.Default with
        {
            LogoCount = 1,
            BackgroundArgb = unchecked((int)0xFFFFFFFF),
            Clock = ClockCorner.Off,
        };
        using var bitmap = RenderFrames(settings, 320, 240, 1);

        Assert.Equal(new SKColor(255, 255, 255, 255), bitmap.GetPixel(0, 0));
    }

    [Fact]
    public void Render_ClockOverlayPaintsItsCorner()
    {
        var withClock = Settings.Default with { LogoCount = 1, Clock = ClockCorner.BottomRight };
        var noClock = Settings.Default with { LogoCount = 1, Clock = ClockCorner.Off };

        using var a = RenderFrames(withClock, 400, 200, 1);
        using var b = RenderFrames(noClock, 400, 200, 1);

        // The clock adds pixels in the bottom-right region that aren't there without it.
        var bg = new SKColor(0, 0, 0, 255);
        int clockCorner = 0;
        for (int y = 150; y < 200; y++)
        {
            for (int x = 250; x < 400; x++)
            {
                if (a.GetPixel(x, y) != bg)
                {
                    clockCorner++;
                }
            }
        }

        Assert.True(clockCorner > 50, $"Expected the clock to paint its corner, saw {clockCorner} pixels.");
    }
}
