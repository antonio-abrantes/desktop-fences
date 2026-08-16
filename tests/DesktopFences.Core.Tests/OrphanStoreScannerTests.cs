using DesktopFences.Core.Models;
using DesktopFences.Core.Persistence;
using DesktopFences.Core.Transactions;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class OrphanStoreScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "df-orphans-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Find_PreservesAndReportsOnlyUnreferencedItemFolders()
    {
        Guid owned = Guid.NewGuid();
        Guid journaled = Guid.NewGuid();
        Guid orphan = Guid.NewGuid();
        Directory.CreateDirectory(Path.Combine(_root, owned.ToString("D")));
        Directory.CreateDirectory(Path.Combine(_root, journaled.ToString("D")));
        string orphanPath = Directory.CreateDirectory(Path.Combine(_root, orphan.ToString("D"))).FullName;
        LayoutDocument layout = new()
        {
            Fences =
            [
                new FenceState
                {
                    Items =
                    [
                        new FenceItemState
                        {
                            ItemId = owned,
                            Kind = FenceItemKind.Stored,
                            Name = "owned.txt",
                            StorageName = "owned.txt"
                        }
                    ]
                }
            ]
        };
        CustodyTransaction transaction = new()
        {
            Items = [new CustodyTransactionItem { ItemId = journaled, Name = "pending.txt" }]
        };

        IReadOnlyList<string> result = OrphanStoreScanner.Find(_root, layout, [transaction]);

        result.Should().Equal(orphanPath);
        Directory.Exists(orphanPath).Should().BeTrue("o scanner nunca exclui dados");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }
}
