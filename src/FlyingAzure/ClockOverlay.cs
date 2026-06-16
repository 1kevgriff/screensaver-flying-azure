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

        var now = DateTime.Now;
        var culture = CultureInfo.CurrentCulture;
        string time = now.ToString("T", culture); // culture long time
        string date = now.ToString("D", culture); // culture long date

        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        SizeF timeSize = g.MeasureString(time, _timeFont);
        SizeF dateSize = g.MeasureString(date, _dateFont);
        float blockW = Math.Max(timeSize.Width, dateSize.Width);
        float blockH = timeSize.Height + dateSize.Height;

        bool right = corner is ClockCorner.TopRight or ClockCorner.BottomRight;
        bool bottom = corner is ClockCorner.BottomLeft or ClockCorner.BottomRight;
        float left = right ? width - Padding - blockW : Padding;
        float top = bottom ? height - Padding - blockH : Padding;

        // Right corners are right-aligned within the block; left corners left-aligned.
        DrawLine(g, time, _timeFont, right ? left + blockW - timeSize.Width : left, top);
        DrawLine(g, date, _dateFont, right ? left + blockW - dateSize.Width : left, top + timeSize.Height);
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
