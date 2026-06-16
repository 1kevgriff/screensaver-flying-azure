using FlyingAzure.Core;
using Xunit;

namespace FlyingAzure.Core.Tests;

public class CommandLineParserTests
{
    [Fact]
    public void NoArgs_IsConfigure() =>
        Assert.Equal(ScreensaverMode.Configure, CommandLineParser.Parse([]).Mode);

    [Theory]
    [InlineData("/s")]
    [InlineData("-s")]
    [InlineData("/S")]
    public void SFlag_IsRun(string arg) =>
        Assert.Equal(ScreensaverMode.Run, CommandLineParser.Parse([arg]).Mode);

    [Theory]
    [InlineData("/c")]
    [InlineData("/C")]
    public void CFlag_IsConfigure(string arg) =>
        Assert.Equal(ScreensaverMode.Configure, CommandLineParser.Parse([arg]).Mode);

    [Fact]
    public void CFlagWithColonHandle_ParsesHandle()
    {
        var p = CommandLineParser.Parse(["/c:12345"]);
        Assert.Equal(ScreensaverMode.Configure, p.Mode);
        Assert.Equal((nint)12345, p.WindowHandle);
    }

    [Fact]
    public void PFlagWithSpaceHandle_IsPreviewWithHandle()
    {
        var p = CommandLineParser.Parse(["/p", "67890"]);
        Assert.Equal(ScreensaverMode.Preview, p.Mode);
        Assert.Equal((nint)67890, p.WindowHandle);
    }

    [Fact]
    public void UnknownArg_IsConfigure() =>
        Assert.Equal(ScreensaverMode.Configure, CommandLineParser.Parse(["whatever"]).Mode);
}
