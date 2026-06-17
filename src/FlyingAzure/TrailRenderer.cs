using System.Drawing;
using FlyingAzure.Core;

namespace FlyingAzure;

/// <summary>
/// Draws a field of chevrons plus their fading "ghost" trails into one viewport.
///
/// The trail is a few translucent copies behind each logo along the reverse travel
/// direction, blitted from a pre-rasterized <see cref="SpriteCache"/> rather than
/// vector-filled per frame. Cost scales with the number of visible sprites, not the
/// pixel count, so it stays fast on a 5K ultrawide. Sprite positions are in the shared
/// virtual-desktop space; <paramref name="offsetX"/>/<paramref name="offsetY"/> map
/// them into this viewport, and anything outside it is culled.
/// </summary>
internal static class TrailRenderer
{
    private const float GhostSpacingFactor = 0.18f; // gap between ghosts, relative to logo size

    public static void Render(
        Graphics g, int width, int height, Color background,
        IReadOnlyList<Sprite> sprites, float offsetX, float offsetY,
        float dirX, float dirY, SpriteCache cache)
    {
        g.Clear(background);

        int ghostCount = cache.GhostCount;
        foreach (var sprite in sprites)
        {
            float x = sprite.Position.X - offsetX;
            float y = sprite.Position.Y - offsetY;
            float margin = sprite.Size;
            float gap = sprite.Size * GhostSpacingFactor;
            int bucket = cache.BucketOf(sprite.Size); // size is constant per sprite

            for (int k = ghostCount; k >= 1; k--)
            {
                float gx = x - dirX * gap * k;
                float gy = y - dirY * gap * k;
                if (gx < -margin || gx > width + margin || gy < -margin || gy > height + margin)
                {
                    continue;
                }

                cache.Draw(g, gx, gy, bucket, k);
            }

            if (x >= -margin && x <= width + margin && y >= -margin && y <= height + margin)
            {
                cache.Draw(g, x, y, bucket, 0);
            }
        }
    }
}
