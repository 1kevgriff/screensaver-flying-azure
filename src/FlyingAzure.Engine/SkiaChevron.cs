using System.Text.RegularExpressions;
using FlyingAzure.Core;
using SkiaSharp;

namespace FlyingAzure.Engine;

/// <summary>
/// Builds the chevron as an SkiaSharp <see cref="SKPath"/> in a centered unit square,
/// reusing the portable <see cref="SvgPathParser"/> + <see cref="PathGeometry"/> from Core
/// (the same geometry the Windows GDI+ renderer uses). Only the rasterizer differs per platform.
/// </summary>
internal static partial class SkiaChevron
{
    public static readonly SKColor AzureBlue = new(0x00, 0xAB, 0xEC);

    public static SKPath BuildUnitPath(string svgPathData)
    {
        var normalized = PathGeometry.Normalize(SvgPathParser.Parse(svgPathData));
        var path = new SKPath { FillType = SKPathFillType.Winding };
        foreach (var sub in normalized)
        {
            bool first = true;
            foreach (var p in sub)
            {
                if (first)
                {
                    path.MoveTo(p.X, p.Y);
                    first = false;
                }
                else
                {
                    path.LineTo(p.X, p.Y);
                }
            }

            path.Close();
        }

        return path;
    }

    public static string LoadEmbeddedSvgPathData()
    {
        using var stream = typeof(SkiaChevron).Assembly.GetManifestResourceStream("azure-chevron.svg")
            ?? throw new InvalidOperationException("Embedded resource 'azure-chevron.svg' was not found.");
        using var reader = new StreamReader(stream);
        string svg = reader.ReadToEnd();

        var match = PathDataRegex().Match(svg);
        if (!match.Success)
        {
            throw new FormatException("No path data found in chevron SVG.");
        }

        return match.Groups[1].Value;
    }

    [GeneratedRegex("d=\"([^\"]+)\"")]
    private static partial Regex PathDataRegex();
}
