using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FlyingAzure.Core;

public static partial class SvgPathParser
{
    [GeneratedRegex(@"[A-Za-z]|-?\d*\.?\d+(?:[eE][-+]?\d+)?")]
    private static partial Regex Token();

    public static IReadOnlyList<IReadOnlyList<PointF>> Parse(string d)
    {
        var tokens = Token().Matches(d).Select(m => m.Value).ToList();
        var subpaths = new List<List<PointF>>();
        List<PointF>? current = null;
        float x = 0, y = 0, startX = 0, startY = 0;
        char cmd = '\0';
        int i = 0;

        float Num() => float.Parse(tokens[i++], CultureInfo.InvariantCulture);
        static bool IsCmd(string t) => t.Length == 1 && char.IsLetter(t[0]);

        while (i < tokens.Count)
        {
            if (IsCmd(tokens[i]))
            {
                cmd = tokens[i][0];
                i++;
            }

            switch (cmd)
            {
                case 'M':
                case 'm':
                    float mx = Num(), my = Num();
                    if (cmd == 'm' && current is not null) { mx += x; my += y; }
                    x = mx; y = my; startX = x; startY = y;
                    current = [new PointF(x, y)];
                    subpaths.Add(current);
                    cmd = cmd == 'M' ? 'L' : 'l';
                    break;
                case 'L':
                case 'l':
                    float lx = Num(), ly = Num();
                    if (cmd == 'l') { lx += x; ly += y; }
                    x = lx; y = ly;
                    current!.Add(new PointF(x, y));
                    break;
                case 'H':
                case 'h':
                    float hx = Num();
                    if (cmd == 'h') hx += x;
                    x = hx;
                    current!.Add(new PointF(x, y));
                    break;
                case 'V':
                case 'v':
                    float vy = Num();
                    if (cmd == 'v') vy += y;
                    y = vy;
                    current!.Add(new PointF(x, y));
                    break;
                case 'Z':
                case 'z':
                    x = startX; y = startY;
                    current = null;
                    cmd = '\0';
                    break;
                default:
                    throw new FormatException($"Unsupported SVG path command '{cmd}'.");
            }
        }

        return subpaths;
    }
}
