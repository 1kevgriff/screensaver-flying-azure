namespace FlyingAzure.Core;

public interface ISettingsBackend
{
    string? Read(string key);
    void Write(string key, string value);
}
