using DesktopFences.Core.Models;
using DesktopFences.Core.Occupancy;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class IconOccupancyTests
{
    private static readonly FenceBounds Fence = new(100, 100, 200, 150);

    [Fact]
    public void Inside_Includes_IconWhoseCellIntersectsFence()
    {
        var icons = new[]
        {
            new DesktopIcon(0, "Inside", 120, 110),
            new DesktopIcon(1, "Outside", 800, 800)
        };

        IReadOnlyList<DesktopIcon> result = IconOccupancy.Inside(icons, Fence);

        result.Should().ContainSingle().Which.Name.Should().Be("Inside");
    }

    [Fact]
    public void Inside_Includes_IconThatOnlyOverlapsTheEdge()
    {
        // Célula 76×92 começando em (50,50) invade o fence em (100,100).
        var icons = new[] { new DesktopIcon(0, "Edge", 50, 50) };

        IconOccupancy.Inside(icons, Fence).Should().ContainSingle();
    }

    [Fact]
    public void Inside_Excludes_IconCompletelyToTheLeft()
    {
        var icons = new[] { new DesktopIcon(0, "Left", 0, 100) };

        IconOccupancy.Inside(icons, Fence).Should().BeEmpty();
    }

    [Fact]
    public void Hit_ReturnsIconWhoseCellContainsThePoint()
    {
        var icons = new[]
        {
            new DesktopIcon(0, "A", 0, 0),
            new DesktopIcon(1, "B", 100, 0)
        };

        IconOccupancy.Hit(icons, 110, 10)!.Name.Should().Be("B");
    }

    [Fact]
    public void Hit_ReturnsNullWhenPointIsInTheGutter()
    {
        var icons = new[] { new DesktopIcon(0, "A", 0, 0) };

        IconOccupancy.Hit(icons, 80, 10).Should().BeNull();
    }

    [Fact]
    public void HitOrNearest_PicksTheClosestIconWhenClickMissesTheCell()
    {
        var icons = new[] { new DesktopIcon(0, "A", 0, 0) };

        IconOccupancy.HitOrNearest(icons, 80, 10, maxDistance: 80)!.Name.Should().Be("A");
    }

    [Fact]
    public void HitOrNearest_IgnoresIconsParkedOffTheDesktop()
    {
        var icons = new[]
        {
            new DesktopIcon(0, "Hidden", -32000, -32000),
            new DesktopIcon(1, "Visible", 0, 0)
        };

        IconOccupancy.HitOrNearest(icons, 10, 10)!.Name.Should().Be("Visible");
    }
}
