using Microsoft.Win32;
using FlyingAzure.Core;
using Xunit;

namespace FlyingAzure.Core.Tests;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class RegistrySettingsBackendTests : IDisposable
{
    private const string TestSubKey = @"Software\FlyingAzure_Test_RoundTrip";

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(TestSubKey, throwOnMissingSubKey: false);
    }

    [Fact]
    public void Read_MissingKey_ReturnsNull()
    {
        var backend = new RegistrySettingsBackend(TestSubKey);
        Assert.Null(backend.Read("NonExistentKey"));
    }

    [Fact]
    public void Write_ThenRead_RoundTripsValue()
    {
        var backend = new RegistrySettingsBackend(TestSubKey);

        backend.Write("Speed", "42");
        backend.Write("LogoCount", "15");

        Assert.Equal("42", backend.Read("Speed"));
        Assert.Equal("15", backend.Read("LogoCount"));
    }
}
