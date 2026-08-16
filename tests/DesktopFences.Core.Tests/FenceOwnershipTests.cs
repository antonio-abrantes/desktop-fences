using DesktopFences.Core.Fences;
using DesktopFences.Core.Models;
using DesktopFences.Core.Persistence;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class FenceOwnershipTests
{
    [Fact]
    public void Transfer_ChangesOnlyFenceOwnershipAndOrder()
    {
        Guid sourceId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        FenceItemState item = Stored("arquivo.txt");
        LayoutDocument original = new()
        {
            Revision = 4,
            Fences =
            [
                new FenceState { Id = sourceId, Title = "A", Items = [item] },
                new FenceState { Id = targetId, Title = "B", Items = [Stored("antes.txt")] }
            ]
        };

        LayoutDocument result = FenceOwnership.Transfer(original, sourceId, targetId, [item.ItemId], 1);

        original.Fences[0].Items.Should().ContainSingle("o documento de entrada não é mutado");
        result.Fences[0].Items.Should().BeEmpty();
        FenceItemState moved = result.Fences[1].Items[1];
        moved.ItemId.Should().Be(item.ItemId);
        moved.StorageName.Should().Be(item.StorageName);
        moved.OriginalPath.Should().Be(item.OriginalPath);
        result.Revision.Should().Be(4);
    }

    [Fact]
    public void Transfer_IsRejectedWhenItemDoesNotBelongToSource()
    {
        LayoutDocument doc = new()
        {
            Fences = [new FenceState { Id = Guid.NewGuid() }, new FenceState { Id = Guid.NewGuid() }]
        };

        Action act = () => FenceOwnership.Transfer(
            doc, doc.Fences[0].Id, doc.Fences[1].Id, [Guid.NewGuid()], 0);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Transfer_OneHundredItemsPreservesEveryStableStorageReference()
    {
        Guid sourceId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        List<FenceItemState> items = Enumerable.Range(1, 100)
            .Select(i => Stored($"item-{i}.txt"))
            .ToList();
        LayoutDocument doc = new()
        {
            Fences =
            [
                new FenceState { Id = sourceId, Items = items },
                new FenceState { Id = targetId }
            ]
        };
        Dictionary<Guid, string?> storageBefore = items.ToDictionary(i => i.ItemId, i => i.StorageName);

        LayoutDocument result = FenceOwnership.Transfer(
            doc, sourceId, targetId, items.Select(i => i.ItemId).ToList(), 0);

        result.Fences[0].Items.Should().BeEmpty();
        result.Fences[1].Items.Should().HaveCount(100);
        result.Fences[1].Items.Should().OnlyContain(i => storageBefore[i.ItemId] == i.StorageName);
    }

    [Fact]
    public void Transfer_SaveFailureLeavesOriginalOwnershipUntouched()
    {
        Guid sourceId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        FenceItemState item = Stored("arquivo.txt");
        LayoutDocument original = new()
        {
            Fences =
            [
                new FenceState { Id = sourceId, Items = [item] },
                new FenceState { Id = targetId }
            ]
        };
        LayoutDocument prospective = FenceOwnership.Transfer(
            original, sourceId, targetId, [item.ItemId], 0);
        prospective.Fences[1].Items[0].ItemId = Guid.Empty;
        string file = Path.Combine(Path.GetTempPath(), "df-transfer-" + Guid.NewGuid().ToString("N"), "layout.json");

        Action save = () => new LayoutStore(file).Save(prospective);

        save.Should().Throw<InvalidDataException>();
        original.Fences.Single(f => f.Id == sourceId).Items.Should().ContainSingle();
        original.Fences.Single(f => f.Id == targetId).Items.Should().BeEmpty();
    }

    private static FenceItemState Stored(string name) => new()
    {
        ItemId = Guid.NewGuid(),
        Kind = FenceItemKind.Stored,
        Name = name,
        StorageName = name,
        OriginalPath = Path.Combine(@"C:\Users\Test\Desktop", name)
    };
}
