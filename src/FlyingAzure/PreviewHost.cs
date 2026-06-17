using System.Drawing;
using System.Runtime.InteropServices;
using FlyingAzure.Core;

namespace FlyingAzure;

public static partial class PreviewHost
{
    private const int GWL_STYLE = -16;
    private const long WS_CHILD = 0x40000000L;
    private const long WS_POPUP = 0x80000000L;

    public static void Run(Settings settings, nint parentHandle)
    {
        if (parentHandle == 0 || !IsWindow(parentHandle))
        {
            return;
        }

        using var renderer = ChevronRenderer.FromEmbeddedAsset();
        GetClientRect(parentHandle, out RECT rc);

        var form = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            BackColor = settings.BackgroundColor(),
        };
        var surface = new AnimatedSurface(settings, renderer) { Dock = DockStyle.Fill };
        form.Controls.Add(surface);

        form.Load += (_, _) =>
        {
            long style = GetWindowLongPtr(form.Handle, GWL_STYLE).ToInt64();
            style = (style | WS_CHILD) & ~WS_POPUP;
            SetWindowLongPtr(form.Handle, GWL_STYLE, (nint)style);
            SetParent(form.Handle, parentHandle);
            form.Location = Point.Empty;
            form.Size = new Size(rc.Right - rc.Left, rc.Bottom - rc.Top);
        };

        using var watchdog = new System.Windows.Forms.Timer { Interval = 500 };
        watchdog.Tick += (_, _) =>
        {
            if (!IsWindow(parentHandle))
            {
                Application.Exit();
            }
        };
        watchdog.Start();
        form.FormClosed += (_, _) => watchdog.Stop();

        using (form)
        {
            Application.Run(form);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [LibraryImport("user32.dll")]
    private static partial nint SetParent(nint child, nint parent);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(nint hWnd, out RECT rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindow(nint hWnd);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(nint hWnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr(nint hWnd, int index, nint value);
}
