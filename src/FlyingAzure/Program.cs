using System.Runtime.InteropServices;
using FlyingAzure.Core;
using Microsoft.Extensions.Logging;

namespace FlyingAzure;

internal static partial class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddProvider(new FileLoggerProvider(LogFilePath())));
        ILogger logger = loggerFactory.CreateLogger("FlyingAzure");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.LogError(e.ExceptionObject as Exception, "Unhandled exception (terminating={Terminating})", e.IsTerminating);
        Application.ThreadException += (_, e) =>
            logger.LogError(e.Exception, "Unhandled UI thread exception");

        Application.EnableVisualStyles();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetCompatibleTextRenderingDefault(false);

        var parsed = CommandLineParser.Parse(args);
        var settings = new SettingsStore(new RegistrySettingsBackend()).Load();
        logger.LogInformation("Starting in {Mode} mode (handle={Handle})", parsed.Mode, parsed.WindowHandle);

        switch (parsed.Mode)
        {
            case ScreensaverMode.Preview:
                PreviewHost.Run(settings, parsed.WindowHandle);
                return 0;
            case ScreensaverMode.Configure:
            {
                using var renderer = ChevronRenderer.FromEmbeddedAsset();
                var store = new SettingsStore(new RegistrySettingsBackend());
                using var form = new ConfigForm(settings, store, renderer);
                Application.Run(form);
                return 0;
            }
            case ScreensaverMode.Run:
            default:
                RunScreensaver(settings, logger);
                return 0;
        }
    }

    private const float TravelAngleDegrees = 150f; // down-left

    private static void RunScreensaver(Settings settings, ILogger logger)
    {
        // One simulation across the whole virtual desktop, so logos flow between monitors.
        var virtualScreen = SystemInformation.VirtualScreen;
        var simulation = new Simulation(virtualScreen.Width, virtualScreen.Height, settings.LogoCount,
            TravelAngleDegrees, settings.SpeedPixelsPerSecond(), settings.MinLogoSizePixels(), settings.MaxLogoSizePixels(), new Random());
        var (dirX, dirY) = simulation.Direction();

        using var renderer = ChevronRenderer.FromEmbeddedAsset();
        using var cache = renderer.CreateSpriteCache(settings.MinLogoSizePixels(), settings.MaxLogoSizePixels(), settings.GhostCount());

        var forms = new List<ScreensaverForm>();
        foreach (var screen in Screen.AllScreens)
        {
            // Each window renders the field offset by its position within the virtual desktop.
            float offsetX = screen.Bounds.X - virtualScreen.X;
            float offsetY = screen.Bounds.Y - virtualScreen.Y;
            var form = new ScreensaverForm(settings, screen.Bounds, offsetX, offsetY, dirX, dirY, cache);
            form.ExitRequested += (_, _) => CloseAll(forms);
            forms.Add(form);
        }

        // Render via an Application.Idle game loop instead of a WinForms Timer: the timer
        // is capped at the ~15.6ms system tick (~60fps ceiling that we never reach after
        // work), whereas the idle loop renders back-to-back, throttled to a 60fps target.
        const double targetFrameMs = 1000.0 / 60.0;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var work = new System.Diagnostics.Stopwatch();
        long lastMs = 0;
        int frames = 0;
        bool fpsLogged = false;
        double nextFrameMs = 0;

        Application.Idle += (_, _) =>
        {
            while (IsApplicationIdle())
            {
                double tMs = clock.ElapsedMilliseconds;
                if (tMs < nextFrameMs)
                {
                    System.Threading.Thread.Sleep(1); // throttle to the target frame rate
                    continue;
                }
                nextFrameMs = tMs + targetFrameMs;

                long now = clock.ElapsedMilliseconds;
                double dt = Math.Min((now - lastMs) / 1000.0, 0.1);
                lastMs = now;

                work.Start();
                simulation.Step(dt);
                foreach (var form in forms)
                {
                    if (!form.IsDisposed)
                    {
                        form.RenderFrame(simulation.Sprites);
                    }
                }
                work.Stop();

                // Log a single steady-state performance sample ~3s in, then stop (keeps the log small).
                frames++;
                if (!fpsLogged && now >= 3000)
                {
                    logger.LogInformation("Render {Fps:F0} fps, frame work {WorkMs:F1}ms across {Monitors} monitor(s)",
                        frames * 1000.0 / now, work.Elapsed.TotalMilliseconds / frames, forms.Count);
                    fpsLogged = true;
                }
            }
        };

        foreach (var form in forms)
        {
            form.Show();
        }

        if (forms.Count > 0)
        {
            forms[0].Activate();
        }

        Application.Run();
    }

    private static bool IsApplicationIdle() => !PeekMessage(out _, 0, 0, 0, 0);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public nint Handle;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int PointX;
        public int PointY;
    }

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PeekMessage(out NativeMessage message, nint hWnd, uint filterMin, uint filterMax, uint remove);

    private static string LogFilePath()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlyingAzure");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "flyingazure.log");
    }

    private static void CloseAll(List<ScreensaverForm> forms)
    {
        foreach (var form in forms)
        {
            if (!form.IsDisposed)
            {
                form.Close();
            }
        }

        Application.Exit();
    }
}
