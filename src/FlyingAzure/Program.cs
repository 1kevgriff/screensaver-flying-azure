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
                // Implemented in Task 9.
                return 0;
            case ScreensaverMode.Configure:
                // Implemented in Task 10.
                return 0;
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
