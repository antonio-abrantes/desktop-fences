using DesktopFences.App.ViewModels;
using DesktopFences.Core.Models;
using DesktopFences.Core.Recovery;
using DesktopFences.Native;
using FluentAssertions;
using Xunit;

namespace DesktopFences.App.Tests;

public sealed class DesktopPlacementTests
{
    [Fact]
    public void BuildRestoredPlacements_SingleItemUsesExactDropPoint()
    {
        FenceItemVm item = Item("Arquivo.txt", 10, 20);

        DesktopPlacement placement = FenceWindow.BuildRestoredPlacements(
            [item], 1200, 700).Single();

        placement.ScreenX.Should().Be(1200);
        placement.ScreenY.Should().Be(700);
        placement.OriginalX.Should().Be(10);
        placement.OriginalY.Should().Be(20);
    }

    [Fact]
    public void BuildRestoredPlacements_MultipleItemsUseDistinctNearbyPoints()
    {
        FenceItemVm[] items =
        [
            Item("A.txt", 1, 2),
            Item("B.txt", 3, 4),
            Item("C.txt", 5, 6),
            Item("D.txt", 7, 8)
        ];

        IReadOnlyList<DesktopPlacement> placements =
            FenceWindow.BuildRestoredPlacements(items, 500, 400);

        placements.Select(item => (item.ScreenX, item.ScreenY)).Should().OnlyHaveUniqueItems();
        placements.Should().OnlyContain(item =>
            item.ScreenX >= 500 && item.ScreenX <= 588
            && item.ScreenY >= 400 && item.ScreenY <= 496);
    }

    [Fact]
    public void BuildRestoredPlacements_WithoutDropPointKeepsOriginalCoordinates()
    {
        DesktopPlacement placement = FenceWindow.BuildRestoredPlacements(
            [Item("Arquivo.txt", 45, 67)], null, null).Single();

        placement.ScreenX.Should().BeNull();
        placement.ScreenY.Should().BeNull();
        placement.OriginalX.Should().Be(45);
        placement.OriginalY.Should().Be(67);
    }

    [Fact]
    public void BuildReleasedPlacements_UsesActualRestoredDestination()
    {
        Guid id = Guid.NewGuid();
        var item = new FenceItemState
        {
            ItemId = id,
            Kind = FenceItemKind.Stored,
            Name = "Atalho",
            OriginalPath = @"C:\Users\Public\Desktop\Atalho.lnk",
            OriginalX = 321,
            OriginalY = 654
        };
        var plan = new DesktopCustodyPlan(
            id,
            FenceItemKind.Stored,
            item.Name,
            @"C:\Store\Atalho.lnk",
            @"C:\Users\Teste\Desktop\Atalho.lnk",
            item.OriginalPath,
            "Atalho.lnk",
            null);

        DesktopPlacement placement = FenceHost.BuildReleasedPlacements(
            [item], [plan], null).Single();

        placement.NameOrPath.Should().Be(plan.DestinationPath);
        placement.OriginalX.Should().Be(321);
        placement.OriginalY.Should().Be(654);
    }

    [Fact]
    public void BuildReleasedPlacements_UsesSnapshotWhenLayoutHasNoCoordinates()
    {
        Guid id = Guid.NewGuid();
        var item = new FenceItemState
        {
            ItemId = id,
            Kind = FenceItemKind.Stored,
            Name = "Arquivo.txt",
            OriginalPath = @"C:\Users\Teste\Desktop\Arquivo.txt"
        };
        var snapshot = new DesktopRecoverySnapshot
        {
            Items =
            [
                new DesktopRecoveryItem
                {
                    ItemId = id,
                    Name = item.Name,
                    OriginalPath = item.OriginalPath,
                    X = 90,
                    Y = 180
                }
            ]
        };

        DesktopPlacement placement = FenceHost.BuildReleasedPlacements(
            [item], [], snapshot).Single();

        placement.OriginalX.Should().Be(90);
        placement.OriginalY.Should().Be(180);
    }

    [Fact]
    public void RunPlacementRetries_WaitsForExplorerAndStopsAfterSecondSuccessfulPass()
    {
        var results = new Queue<int>([0, 3]);
        int waits = 0;

        int positioned = FenceHost.RunPlacementRetries(
            () => results.Dequeue(),
            expectedCount: 3,
            wait: () => waits++);

        positioned.Should().Be(3);
        waits.Should().Be(1);
        results.Should().BeEmpty();
    }

    [Fact]
    public void RunPlacementRetries_IsBoundedWhenExplorerCannotMatchAnItem()
    {
        int attempts = 0;
        int waits = 0;

        int positioned = FenceHost.RunPlacementRetries(
            () => { attempts++; return 1; },
            expectedCount: 2,
            maxAttempts: 4,
            wait: () => waits++);

        positioned.Should().Be(1);
        attempts.Should().Be(4);
        waits.Should().Be(3);
    }

    private static FenceItemVm Item(string name, int x, int y) => new()
    {
        Name = name,
        Path = name,
        OriginalX = x,
        OriginalY = y
    };
}
