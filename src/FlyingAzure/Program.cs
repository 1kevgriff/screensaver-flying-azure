using FlyingAzure.Core;
using Microsoft.Extensions.Logging;

namespace FlyingAzure;

internal static class Program
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
                RunScreensaver(settings);
                return 0;
        }
    }

    private static void RunScreensaver(Settings settings)
    {
        using var renderer = ChevronRenderer.FromEmbeddedAsset();
        var forms = new List<ScreensaverForm>();

        foreach (var screen in Screen.AllScreens)
        {
            var form = new ScreensaverForm(settings, screen.Bounds, renderer);
            form.ExitRequested += (_, _) => CloseAll(forms);
            forms.Add(form);
        }

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
