using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using FlyingAzure.Core;

namespace FlyingAzure;

/// <summary>
/// Draws a date/time overlay (formatted in the machine's current culture) in a chosen
/// corner of a surface. Time on top, date beneath; a soft shadow keeps it legible over
/// any background. Fonts are sized to the surface height and cached for the surface's life.
/// </summary>
public sealed class ClockOverlay : IDisposable
{
    private const int Padding = 28;

    private readonly Font _timeFont;
    private readonly Font _dateFont;
    private readonly Brush _fill = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
    private readonly Brush _shadow = new SolidBrush(Color.FromArgb(150, 0, 0, 0));

    // The displayed text changes only once per second, so the formatted strings and their
    // measured sizes are cached and refreshed on a second boundary rather than every frame.
    private int _cachedSecond = -1;
    private string _time = string.Empty;
    private string _date = string.Empty;
    private SizeF _timeSize;
    private SizeF _dateSize;

    public ClockOverlay(int surfaceHeight)
    {
        float timePx = Math.Clamp(surfaceHeight * 0.030f, 16f, 44f);
        _timeFont = new Font("Segoe UI", timePx, FontStyle.Bold, GraphicsUnit.Pixel);
        _dateFont = new Font("Segoe UI", timePx * 0.5f, FontStyle.Regular, GraphicsUnit.Pixel);
    }

    public void Draw(Graphics g, int width, int height, ClockCorner corner)
    {
        if (corner == ClockCorner.Off)
        {
            return;
        }

        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        var now = DateTime.Now;
        if (now.Second != _cachedSecond)
        {
            _cachedSecond = now.Second;
            var culture = CultureInfo.CurrentCulture;
            _time = now.ToString("T", culture); // culture long time
            _date = now.ToString("D", culture); // culture long date
            _timeSize = g.MeasureString(_time, _timeFont);
            _dateSize = g.MeasureString(_date, _dateFont);
        }

        float blockW = Math.Max(_timeSize.Width, _dateSize.Width);
        float blockH = _timeSize.Height + _dateSize.Height;

        bool right = corner is ClockCorner.TopRight or ClockCorner.BottomRight;
        bool bottom = corner is ClockCorner.BottomLeft or ClockCorner.BottomRight;
        float left = right ? width - Padding - blockW : Padding;
        float top = bottom ? height - Padding - blockH : Padding;

        // Right corners are right-aligned within the block; left corners left-aligned.
        DrawLine(g, _time, _timeFont, right ? left + blockW - _timeSize.Width : left, top);
        DrawLine(g, _date, _dateFont, right ? left + blockW - _dateSize.Width : left, top + _timeSize.Height);
    }

    private void DrawLine(Graphics g, string text, Font font, float x, float y)
    {
        g.DrawString(text, font, _shadow, x + 2f, y + 2f);
        g.DrawString(text, font, _fill, x, y);
    }

    public void Dispose()
    {
        _timeFont.Dispose();
        _dateFont.Dispose();
        _fill.Dispose();
        _shadow.Dispose();
    }
}
