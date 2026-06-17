using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace FlyingAzure;

/// <summary>
/// Pre-rasterizes the chevron into a grid of bitmaps — one per (size bucket, alpha
/// level) — so the render loop blits ready-made sprites with <c>DrawImageUnscaled</c>
/// instead of rasterizing an anti-aliased vector path every frame. Blitting is an
/// order of magnitude cheaper than per-frame <c>FillPath</c>, which is what lets the
/// ghost-trail field stay at 60fps on large/multiple monitors.
/// </summary>
public sealed class SpriteCache : IDisposable
{
    public const int GhostMaxAlpha = 110;

    private const int Buckets = 10;

    private readonly Bitmap[][] _bitmaps; // [sizeBucket][level] : level 0 = full sprite, 1..GhostCount = ghosts
    private readonly float _minSize;
    private readonly float _range;

    public int GhostCount { get; }

    public SpriteCache(GraphicsPath unitPath, Color color, float minSize, float maxSize, int ghostCount)
    {
        GhostCount = Math.Max(0, ghostCount);
        _minSize = minSize;
        _range = Math.Max(1f, maxSize - minSize);

        _bitmaps = new Bitmap[Buckets][];
        for (int bucket = 0; bucket < Buckets; bucket++)
        {
            float size = minSize + _range * bucket / (Buckets - 1);
            int dim = Math.Max(1, (int)MathF.Ceiling(size)) + 2; // +2 px padding so anti-aliased edges aren't clipped

            _bitmaps[bucket] = new Bitmap[GhostCount + 1];
            for (int level = 0; level <= GhostCount; level++)
            {
                int alpha = level == 0 ? 255 : GhostMaxAlpha * (GhostCount - level + 1) / (GhostCount + 1);
                var bmp = new Bitmap(dim, dim, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using var brush = new SolidBrush(Color.FromArgb(alpha, color));
                    g.TranslateTransform(dim / 2f, dim / 2f);
                    g.ScaleTransform(size, size);
                    g.FillPath(brush, unitPath);
                }

                _bitmaps[bucket][level] = bmp;
            }
        }
    }

    /// <summary>Maps a sprite size to its cached bucket. A sprite's size is constant, so
    /// callers resolve the bucket once per sprite and reuse it for the sprite and its ghosts.</summary>
    public int BucketOf(float size)
    {
        int b = (int)MathF.Round((size - _minSize) / _range * (Buckets - 1));
        return Math.Clamp(b, 0, Buckets - 1);
    }

    /// <summary>Blits the cached sprite centered at (cx, cy). <paramref name="level"/> 0 is the
    /// full sprite; 1..<see cref="GhostCount"/> are progressively fainter ghosts.</summary>
    public void Draw(Graphics g, float cx, float cy, int bucket, int level)
    {
        var bmp = _bitmaps[bucket][level];
        g.DrawImageUnscaled(bmp, (int)(cx - bmp.Width / 2f), (int)(cy - bmp.Height / 2f));
    }

    public void Dispose()
    {
        foreach (var row in _bitmaps)
        {
            foreach (var bmp in row)
            {
                bmp.Dispose();
            }
        }
    }
}
