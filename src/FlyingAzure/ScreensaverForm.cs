using System.Drawing;
using FlyingAzure.Core;

namespace FlyingAzure;

public sealed class ScreensaverForm : Form
{
    private const int MouseDeadZonePixels = 8;

    private Point _initialMouse;
    private bool _mouseInitialized;
    private bool _exiting;

    public event EventHandler? ExitRequested;

    public ScreensaverForm(Settings settings, Rectangle bounds, ChevronRenderer renderer)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = settings.BackgroundColor();
        KeyPreview = true;
        DoubleBuffered = true;

        var surface = new AnimatedSurface(settings, renderer) { Dock = DockStyle.Fill };
        surface.MouseMove += (_, _) => OnUserMouseMove();
        surface.MouseDown += (_, _) => RequestExit();
        Controls.Add(surface);

        Cursor.Hide();
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
        }

        base.Dispose(disposing);
    }
}
