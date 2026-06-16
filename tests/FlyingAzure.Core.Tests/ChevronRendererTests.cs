using Xunit;

namespace FlyingAzure.Core.Tests;

public class ChevronRendererTests
{
    [Fact]
    public void FromEmbeddedAsset_BuildsUnitWidthPath()
    {
        using var renderer = FlyingAzure.ChevronRenderer.FromEmbeddedAsset();
        var bounds = renderer.Path.GetBounds();
        // The chevron is 16 wide (the larger dimension), so normalized width ≈ 1.0.
        Assert.InRange(bounds.Width, 0.98f, 1.02f);
        Assert.InRange(bounds.Height, 0.6f, 0.9f);
    }
}
