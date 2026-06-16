using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using FlyingAzure.Core;

namespace FlyingAzure;

public sealed partial class ChevronRenderer : IDisposable
{
    private static readonly Color AzureBlue = Color.FromArgb(0x00, 0xAB, 0xEC);

    private readonly GraphicsPath _path;

    public ChevronRenderer(string svgPathData)
    {
        var normalized = PathGeometry.Normalize(SvgPathParser.Parse(svgPathData));
        _path = new GraphicsPath { FillMode = FillMode.Winding };
        foreach (var sub in normalized)
        {
            _path.AddPolygon(sub.ToArray());
        }
    }

    public GraphicsPath Path => _path;

    /// <summary>Pre-rasterizes the chevron into a <see cref="SpriteCache"/> for fast per-frame blitting.</summary>
    public SpriteCache CreateSpriteCache(float minSize, float maxSize, int ghostCount) =>
        new(_path, AzureBlue, minSize, maxSize, ghostCount);

    public static ChevronRenderer FromEmbeddedAsset()
    {
        using var stream = typeof(ChevronRenderer).Assembly.GetManifestResourceStream("azure-chevron.svg")
            ?? throw new InvalidOperationException("Embedded resource 'azure-chevron.svg' was not found.");
        using var reader = new StreamReader(stream);
        string svg = reader.ReadToEnd();

        var match = PathDataRegex().Match(svg);
        if (!match.Success)
        {
            throw new FormatException("No path data found in chevron SVG.");
        }

        return new ChevronRenderer(match.Groups[1].Value);
    }

    [GeneratedRegex("d=\"([^\"]+)\"")]
    private static partial Regex PathDataRegex();

    public void Dispose()
    {
        _path.Dispose();
    }
}
