using FlyingAzure.Core;

namespace FlyingAzure;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetCompatibleTextRenderingDefault(false);

        var parsed = CommandLineParser.Parse(args);
        var settings = new SettingsStore(new RegistrySettingsBackend()).Load();

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
