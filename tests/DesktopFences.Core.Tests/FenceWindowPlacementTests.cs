using DesktopFences.Core.Fences;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class FenceWindowPlacementTests
{
    [Fact]
    public void ClampToWorkArea_KeepsPosition_WhenInside()
    {
        (double x, double y) = FenceWindowPlacement.ClampToWorkArea(100, 80, 400, 280, 0, 0, 1920, 1040);
        x.Should().Be(100);
        y.Should().Be(80);
    }

    [Fact]
    public void ClampToWorkArea_PullsInside_WhenPartiallyOutside()
    {
        (double x, double y) = FenceWindowPlacement.ClampToWorkArea(-20, 900, 400, 280, 0, 0, 1920, 1040);
        x.Should().Be(0);
        y.Should().Be(760);
    }
}
