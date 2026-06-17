using System.Globalization;
using FlyingAzure.Core;
using SkiaSharp;

namespace FlyingAzure.Engine;

/// <summary>
/// SkiaSharp port of the Windows <c>ClockOverlay</c>: draws a culture-formatted date/time in a
/// chosen corner (time on top, date beneath) with a soft shadow. Formatted strings + measurements
/// are cached on a one-second boundary rather than recomputed every frame. Uses the platform
/// default UI typeface (Segoe UI on Windows, San Francisco on macOS) via a null family request.
/// </summary>
internal sealed class SkiaClockOverlay : IDisposable
{
    private readonly float _padding;
    private readonly SKFont _timeFont;
    private readonly SKFont _dateFont;
    private readonly SKPaint _fill = new() { Color = new SKColor(255, 255, 255, 235), IsAntialias = true };
    private readonly SKPaint _shadow = new() { Color = new SKColor(0, 0, 0, 150), IsAntialias = true };
    private readonly SKTypeface _bold;
    private readonly SKTypeface _regular;

    private long _cachedSecondStamp = -1;
    private string _time = string.Empty;
    private string _date = string.Empty;
    private float _timeWidth;
    private float _dateWidth;

    public SkiaClockOverlay(int surfaceHeight)
    {
        float timePx = Math.Clamp(surfaceHeight * 0.030f, 16f, 44f);
        _padding = Math.Clamp(surfaceHeight * 0.022f, 12f, 40f);
        _bold = SKTypeface.FromFamilyName(null, SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            ?? SKTypeface.Default;
        _regular = SKTypeface.FromFamilyName(null, SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            ?? SKTypeface.Default;
        _timeFont = new SKFont(_bold, timePx);
        _dateFont = new SKFont(_regular, timePx * 0.5f);
    }

    public void Draw(SKCanvas canvas, int width, int height, ClockCorner corner)
    {
        if (corner == ClockCorner.Off)
        {
            return;
        }

        var now = DateTime.Now;
        long secondStamp = now.Ticks / TimeSpan.TicksPerSecond;
        if (secondStamp != _cachedSecondStamp)
        {
            _cachedSecondStamp = secondStamp;
            var culture = CultureInfo.CurrentCulture;
            _time = now.ToString("T", culture);
            _date = now.ToString("D", culture);
            _timeWidth = _timeFont.MeasureText(_time);
            _dateWidth = _dateFont.MeasureText(_date);
        }

        float timeLineH = _timeFont.Size * 1.2f;
        float dateLineH = _dateFont.Size * 1.2f;
        float blockW = Math.Max(_timeWidth, _dateWidth);
        float blockH = timeLineH + dateLineH;

        bool right = corner is ClockCorner.TopRight or ClockCorner.BottomRight;
        bool bottom = corner is ClockCorner.BottomLeft or ClockCorner.BottomRight;
        float left = Math.Clamp(right ? width - _padding - blockW : _padding, 0f, Math.Max(0f, width - blockW));
        float top = Math.Clamp(bottom ? height - _padding - blockH : _padding, 0f, Math.Max(0f, height - blockH));

        DrawLine(canvas, _time, _timeFont, right ? left + blockW - _timeWidth : left, top, timeLineH);
        DrawLine(canvas, _date, _dateFont, right ? left + blockW - _dateWidth : left, top + timeLineH, dateLineH);
    }

    // Skia draws text from the baseline; offset by the font ascent so (x, y) is the line's top-left.
    private void DrawLine(SKCanvas canvas, string text, SKFont font, float x, float y, float lineHeight)
    {
        float baseline = y + (lineHeight - font.Metrics.Descent + font.Metrics.Ascent) / 2f - font.Metrics.Ascent;
        canvas.DrawText(text, x + 2f, baseline + 2f, font, _shadow);
        canvas.DrawText(text, x, baseline, font, _fill);
    }

    public void Dispose()
    {
        _timeFont.Dispose();
        _dateFont.Dispose();
        _fill.Dispose();
        _shadow.Dispose();
        _bold.Dispose();
        _regular.Dispose();
    }
}
