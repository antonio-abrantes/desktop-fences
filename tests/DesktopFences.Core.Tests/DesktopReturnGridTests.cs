using DesktopFences.Core.Models;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class DesktopReturnGridTests
{
    [Fact]
    public void PreserveExisting_RestoresDisplacedItemsAndKeepsPositionsChosenBeforeTheOperation()
    {
        var saved = new Dictionary<string, (int X, int Y)>
        {
            ["Inkscape"] = (2086, 42),
            ["Insomnia"] = (2010, 630),
            ["Manually moved group"] = (3302, 532)
        };
        var verified = new Dictionary<string, (int X, int Y)>
        {
            ["Inkscape"] = (2086, 140),
            ["Insomnia"] = (2010, 728),
            ["Manually moved group"] = (3302, 532)
        };

        var corrections = DesktopReturnGrid.PreserveExisting(saved, verified, [], 76, 98);

        corrections.Should().HaveCount(2);
        corrections["Inkscape"].Should().Be((2086, 42));
        corrections["Insomnia"].Should().Be((2010, 630));
        corrections.Should().NotContainKey("Manually moved group");
    }

    [Fact]
    public void PreserveExisting_DoesNotDisplaceAnUnverifiedReplacementOrItsNeighbours()
    {
        var saved = new Dictionary<string, (int X, int Y)> { ["A"] = (0, 0), ["B"] = (100, 0) };
        var verified = new Dictionary<string, (int X, int Y)> { ["A"] = (100, 0), ["B"] = (200, 0) };

        DesktopReturnGrid.PreserveExisting(saved, verified, [(0, 0)], 100, 100)
            .Should().BeEmpty();
    }

    [Fact]
    public void Reserve_UsesTheDropCellThenNearestDistinctCells()
    {
        var reservations = DesktopReturnGrid.Reserve(
            [((100, 100), "first", false), ((100, 100), "second", false), ((100, 100), "third", false)],
            [(200, 100)],
            [(new PixelRect(0, 0, 400, 300), (0, 0))],
            100, 100);

        reservations["first"].Should().Be((100, 100));
        reservations.Values.Should().OnlyHaveUniqueItems()
            .And.NotContain((200, 100));
        reservations.Values.Should().OnlyContain(point =>
            Math.Abs(point.X - 100) + Math.Abs(point.Y - 100) <= 100);
    }

    [Fact]
    public void Reserve_KeepsAnAvailableOriginalCellForItsOwner()
    {
        var reservations = DesktopReturnGrid.Reserve(
            [((0, 0), "needs a nearby cell", true), ((100, 0), "original available", true)],
            [(0, 0)],
            [(new PixelRect(0, 0, 300, 100), (0, 0))],
            100, 100);

        reservations["original available"].Should().Be((100, 0));
        reservations["needs a nearby cell"].Should().Be((200, 0));
    }

    [Fact]
    public void ReserveAfterReturn_KeepsBothGroupsAndProtectsBothPositionsOfAnExternalIcon()
    {
        var reservations = DesktopReturnGrid.ReserveAfterReturn(
            [((2314, 532), "folder", true), ((2390, 532), "adjacent", true),
             ((2770, 630), "shortcut", true), ((2086, 42), "home was occupied", true)],
            [(2086, 42)], [(2086, 140)],
            [(new PixelRect(1900, 0, 1890, 1009), (2086, 42))],
            76, 98);

        reservations["folder"].Should().Be((2314, 532));
        reservations["adjacent"].Should().Be((2390, 532));
        reservations["shortcut"].Should().Be((2770, 630));
        reservations.Values.Should().NotContain((2086, 42)).And.NotContain((2086, 140));
        reservations["home was occupied"].Should().Be((2010, 42));
    }

    [Fact]
    public void Reserve_KeepsAnOriginalCellOnTheLastVisibleRow()
    {
        var reservations = DesktopReturnGrid.Reserve(
            [((2314, 924), "original", true)], [],
            [(new PixelRect(1900, 0, 1890, 1009), (2314, 42))],
            76, 98);

        reservations["original"].Should().Be((2314, 924));
    }

    [Fact]
    public void Reserve_DefersOnlyItemsThatDoNotFitAndRespectsNegativeMonitorCoordinates()
    {
        var reservations = DesktopReturnGrid.Reserve(
            [((-100, 0), "fits", false), ((-100, 0), "waits", false)],
            [(-201, 0)],
            [(new PixelRect(-300, 0, 300, 100), (-300, 0))],
            100, 100);

        reservations.Should().ContainSingle();
        reservations["fits"].Should().Be((-100, 0));
    }
}
