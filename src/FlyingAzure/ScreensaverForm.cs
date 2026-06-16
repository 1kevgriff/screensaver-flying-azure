using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using FlyingAzure.Core;

namespace FlyingAzure;

/// <summary>
/// A full-screen window for one monitor. It is a passive renderer: a shared
/// <see cref="Simulation"/> (in virtual-desktop coordinates) is stepped elsewhere,
/// and <see cref="RenderFrame"/> draws the slice of that field falling within this
/// monitor — so a logo flows continuously from one monitor onto the next.
/// </summary>
public sealed class ScreensaverForm : Form
{
    private const int MouseDeadZonePixels = 8;

    private readonly SpriteCache _cache; // shared across windows (read-only blits on the UI thread)
    private readonly float _offsetX;
    private readonly float _offsetY;
    private readonly float _dirX;
    private readonly float _dirY;
    private readonly Color _background;
    private readonly BufferedGraphicsContext _bufferContext = new();
    private BufferedGraphics? _backBuffer;
    private Graphics? _present;

    private Point _initialMouse;
    private bool _mouseInitialized;
    private bool _exiting;

    public event EventHandler? ExitRequested;

    public ScreensaverForm(Settings settings, Rectangle bounds, float offsetX, float offsetY,
        float dirX, float dirY, SpriteCache cache)
    {
        _cache = cache;
        _offsetX = offsetX;
        _offsetY = offsetY;
        _dirX = dirX;
        _dirY = dirY;
        _background = settings.BackgroundColor();

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = _background;
        KeyPreview = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque | ControlStyles.UserPaint, true);

        Cursor.Hide();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var size = new Size(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height));
        _bufferContext.MaximumBuffer = new Size(size.Width + 1, size.Height + 1);
        _present = CreateGraphics();
        _backBuffer = _bufferContext.Allocate(_present, new Rectangle(Point.Empty, size));
        _backBuffer.Graphics.Clear(_background);
    }

    /// <summary>
    /// Renders this monitor's slice of the shared sprite field and presents it.
    /// Presents via <see cref="BufferedGraphics.Render()"/>, which uses a GDI BitBlt —
    /// far faster than GDI+ DrawImage to the screen DC at full resolution.
    /// </summary>
    public void RenderFrame(IReadOnlyList<Sprite> sprites)
    {
        if (_backBuffer is null)
        {
            return;
        }

        TrailRenderer.Render(_backBuffer.Graphics, ClientSize.Width, ClientSize.Height, _background,
            sprites, _offsetX, _offsetY, _dirX, _dirY, _cache);

        if (_present is not null)
        {
            _backBuffer.Render(_present);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        _backBuffer?.Render(e.Graphics);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // The buffer owns every pixel.
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        TopMost = true;
        BringToFront();
        Activate();
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e) => RequestExit();

    protected override void OnMouseDown(MouseEventArgs e) => RequestExit();

    protected override void OnMouseMove(MouseEventArgs e) => OnUserMouseMove();

    private void OnUserMouseMove()
    {
        var pos = Cursor.Position;
        if (!_mouseInitialized)
        {
            _initialMouse = pos;
            _mouseInitialized = true;
            return;
        }

        if (Math.Abs(pos.X - _initialMouse.X) > MouseDeadZonePixels ||
            Math.Abs(pos.Y - _initialMouse.Y) > MouseDeadZonePixels)
        {
            RequestExit();
        }
    }

    private void RequestExit()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        Cursor.Show();
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Cursor.Show();
            _present?.Dispose();
            _backBuffer?.Dispose();
            _bufferContext.Dispose();
            // _cache is shared and owned by the caller (RunScreensaver); do not dispose here.
        }

        base.Dispose(disposing);
    }
}
