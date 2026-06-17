using SkiaSharp;

namespace FlyingAzure.Engine;

/// <summary>
/// SkiaSharp port of the Windows <c>SpriteCache</c>: pre-rasterizes the chevron into a grid
/// of <see cref="SKImage"/>s — one per (size bucket, alpha level) — so the render loop blits
/// ready-made sprites instead of filling an anti-aliased path every frame. Same caching
/// strategy and bucket math as the GDI+ version; only the rasterizer is Skia.
/// </summary>
internal sealed class SkiaSpriteCache : IDisposable
{
    public const int GhostMaxAlpha = 110;

    private const int Buckets = 10;

    private readonly SKImage[][] _images; // [sizeBucket][level]: 0 = full sprite, 1..GhostCount = ghosts
    private readonly float _minSize;
    private readonly float _range;

    public int GhostCount { get; }

    public SkiaSpriteCache(SKPath unitPath, SKColor color, float minSize, float maxSize, int ghostCount)
    {
        GhostCount = Math.Max(0, ghostCount);
        _minSize = minSize;
        _range = Math.Max(1f, maxSize - minSize);

        _images = new SKImage[Buckets][];
        for (int bucket = 0; bucket < Buckets; bucket++)
        {
            float size = minSize + _range * bucket / (Buckets - 1);
            int dim = Math.Max(1, (int)MathF.Ceiling(size)) + 2; // +2 px padding so AA edges aren't clipped

            _images[bucket] = new SKImage[GhostCount + 1];
            for (int level = 0; level <= GhostCount; level++)
            {
                byte alpha = (byte)(level == 0 ? 255 : GhostMaxAlpha * (GhostCount - level + 1) / (GhostCount + 1));

                var info = new SKImageInfo(dim, dim, SKColorType.Rgba8888, SKAlphaType.Premul);
                using var surface = SKSurface.Create(info);
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);
                canvas.Translate(dim / 2f, dim / 2f);
                canvas.Scale(size);
                using var paint = new SKPaint
                {
                    Color = color.WithAlpha(alpha),
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill,
                };
                canvas.DrawPath(unitPath, paint);

                _images[bucket][level] = surface.Snapshot();
            }
        }
    }

    /// <summary>Maps a (constant) sprite size to its cached bucket, resolved once per sprite.</summary>
    public int BucketOf(float size)
    {
        int b = (int)MathF.Round((size - _minSize) / _range * (Buckets - 1));
        return Math.Clamp(b, 0, Buckets - 1);
    }

    /// <summary>Blits the cached sprite centered at (cx, cy). Level 0 is the full sprite;
    /// 1..<see cref="GhostCount"/> are progressively fainter ghosts.</summary>
    public void Draw(SKCanvas canvas, float cx, float cy, int bucket, int level)
    {
        var img = _images[bucket][level];
        canvas.DrawImage(img, cx - img.Width / 2f, cy - img.Height / 2f);
    }

    public void Dispose()
    {
        foreach (var row in _images)
        {
            foreach (var img in row)
            {
                img.Dispose();
            }
        }
    }
}
