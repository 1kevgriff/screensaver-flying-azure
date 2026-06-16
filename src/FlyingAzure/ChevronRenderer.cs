using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using FlyingAzure.Core;

namespace FlyingAzure;

public sealed partial class ChevronRenderer : IDisposable
{
    private static readonly Color AzureBlue = Color.FromArgb(0x00, 0xAB, 0xEC);

    private readonly GraphicsPath _path;
    private readonly SolidBrush _brush = new(AzureBlue);

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

    public void Draw(Graphics g, Sprite sprite)
    {
        var state = g.Save();
        try
        {
            g.TranslateTransform(sprite.Position.X, sprite.Position.Y);
            g.ScaleTransform(sprite.Size, sprite.Size);
            g.FillPath(_brush, _path);
        }
        finally
        {
            g.Restore(state);
        }
    }

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
        _brush.Dispose();
    }
}
