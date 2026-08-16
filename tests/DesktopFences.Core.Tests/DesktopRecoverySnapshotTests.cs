using DesktopFences.Core.Models;
using DesktopFences.Core.Recovery;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class DesktopRecoverySnapshotTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "df-recovery-snapshot-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Build_MergesStoredItemsWithVisibleDesktopAndUsesVisiblePositionAsNewest()
    {
        Guid itemId = Guid.NewGuid();
        var layout = new LayoutDocument
        {
            Fences =
            [
                new FenceState
                {
                    Items =
                    [
                        new FenceItemState
                        {
                            ItemId = itemId,
                            Kind = FenceItemKind.Stored,
                            Name = "Projeto",
                            StorageName = "Projeto",
                            OriginalPath = @"C:\Users\Test\Desktop\Projeto",
                            OriginalX = 10,
                            OriginalY = 20
                        }
                    ]
                }
            ]
        };

        DesktopRecoverySnapshot result = DesktopRecoverySnapshotBuilder.Build(
            [new DesktopIcon(0, "Projeto", 300, 400), new DesktopIcon(1, "Solto.txt", 50, 60)],
            layout,
            name => name == "Projeto" ? @"C:\Users\Test\Desktop\Projeto" : @"C:\Users\Test\Desktop\Solto.txt");

        result.Items.Should().HaveCount(2);
        DesktopRecoveryItem project = result.Items.Single(item => item.Name == "Projeto");
        project.ItemId.Should().Be(itemId);
        project.X.Should().Be(300);
        project.Y.Should().Be(400);
    }

    [Fact]
    public void Build_PreservesPreviousItemsMissingDuringCrash()
    {
        var previous = new DesktopRecoverySnapshot
        {
            Items = [new DesktopRecoveryItem { Name = "Guardado.txt", OriginalPath = @"C:\Desktop\Guardado.txt", X = 8, Y = 9 }]
        };

        DesktopRecoverySnapshot result = DesktopRecoverySnapshotBuilder.Build(
            [], new LayoutDocument(), _ => null, previous);

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Guardado.txt");
        result.Items.Single().X.Should().Be(8);
    }

    [Fact]
    public void Store_WritesAtomicallyAndFallsBackToBackup()
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "snapshot.json");
        var store = new DesktopRecoverySnapshotStore(path);
        store.Save(new DesktopRecoverySnapshot { Items = [new DesktopRecoveryItem { Name = "Primeiro", X = 1, Y = 2 }] });
        store.Save(new DesktopRecoverySnapshot { Items = [new DesktopRecoveryItem { Name = "Segundo", X = 3, Y = 4 }] });
        File.WriteAllText(path, "{quebrado");

        DesktopRecoverySnapshot? loaded = store.Load();

        loaded.Should().NotBeNull();
        loaded!.Items.Single().Name.Should().Be("Primeiro");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }
}
