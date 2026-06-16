using System.Globalization;

namespace FlyingAzure.Core;

public sealed class SettingsStore(ISettingsBackend backend)
{
    public Settings Load()
    {
        var d = Settings.Default;
        return new Settings
        {
            LogoCount = ReadInt(nameof(Settings.LogoCount), d.LogoCount),
            Speed = ReadInt(nameof(Settings.Speed), d.Speed),
            Size = ReadInt(nameof(Settings.Size), d.Size),
            TrailLength = ReadInt(nameof(Settings.TrailLength), d.TrailLength),
            BackgroundArgb = ReadInt(nameof(Settings.BackgroundArgb), d.BackgroundArgb),
        }.Clamp();
    }

    public void Save(Settings s)
    {
        s = s.Clamp();
        Write(nameof(Settings.LogoCount), s.LogoCount);
        Write(nameof(Settings.Speed), s.Speed);
        Write(nameof(Settings.Size), s.Size);
        Write(nameof(Settings.TrailLength), s.TrailLength);
        Write(nameof(Settings.BackgroundArgb), s.BackgroundArgb);
    }

    private int ReadInt(string key, int fallback) =>
        int.TryParse(backend.Read(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

    private void Write(string key, int value) =>
        backend.Write(key, value.ToString(CultureInfo.InvariantCulture));
}
