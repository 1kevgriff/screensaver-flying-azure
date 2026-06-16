using Microsoft.Win32;

namespace FlyingAzure.Core;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class RegistrySettingsBackend(string subKey = @"Software\FlyingAzure") : ISettingsBackend
{
    public string? Read(string key)
    {
        using var reg = Registry.CurrentUser.OpenSubKey(subKey);
        return reg?.GetValue(key)?.ToString();
    }

    public void Write(string key, string value)
    {
        using var reg = Registry.CurrentUser.CreateSubKey(subKey);
        reg.SetValue(key, value);
    }
}
