using System.Drawing;

namespace FlyingAzure.Core;

public static class PathGeometry
{
    public static IReadOnlyList<IReadOnlyList<PointF>> Normalize(IReadOnlyList<IReadOnlyList<PointF>> subpaths)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var sub in subpaths)
        {
            foreach (var p in sub)
            {
                minX = Math.Min(minX, p.X);
                minY = Math.Min(minY, p.Y);
                maxX = Math.Max(maxX, p.X);
                maxY = Math.Max(maxY, p.Y);
            }
        }

        float cx = (minX + maxX) / 2f;
        float cy = (minY + maxY) / 2f;
        float span = Math.Max(maxX - minX, maxY - minY);
        float scale = span > 0 ? 1f / span : 1f;

        return subpaths
            .Select(sub => (IReadOnlyList<PointF>)sub
                .Select(p => new PointF((p.X - cx) * scale, (p.Y - cy) * scale))
                .ToList())
            .ToList();
    }
}
