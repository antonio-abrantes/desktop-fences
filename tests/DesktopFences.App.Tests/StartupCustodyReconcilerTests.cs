using System.IO;
using DesktopFences.Core;
using DesktopFences.Core.Models;
using FluentAssertions;
using Xunit;

namespace DesktopFences.App.Tests;

public sealed class StartupCustodyReconcilerTests
{
    private const string ItemsRoot = @"C:\DesktopFences\Items";
    private const string DesktopRoot = @"C:\Users\Test\Desktop";

    [Fact]
    public void Reconcile_RemovesOnlyStoredItemConfirmedMissingFromStoreAndDesktop()
    {
        FenceItemState missing = Stored("apagado.txt");
        FenceItemState available = Stored("presente.txt");
        LayoutDocument source = Document(missing, available);
        string availablePath = FenceItemStore.PayloadPath(ItemsRoot, available.ItemId, available.StorageName!);

        StartupCustodyReconciliation result = Reconcile(
            source,
            path => path == availablePath ? StartupPathState.Present : StartupPathState.Missing);

        result.RemovedItemIds.Should().Equal(missing.ItemId);
        result.Document.Fences[0].Items.Should().ContainSingle(item => item.ItemId == available.ItemId);
        source.Fences[0].Items.Should().HaveCount(2, "a reconciliação não pode alterar o documento carregado antes do commit atômico");
    }

    [Fact]
    public void Reconcile_PreservesNamespaceAndItemFoundOnDesktop()
    {
        FenceItemState desktopItem = Stored("presente.txt");
        var namespaceItem = new FenceItemState
        {
            ItemId = Guid.NewGuid(),
            Kind = FenceItemKind.Namespace,
            Name = "Lixeira",
            OriginalPath = "::{645FF040-5081-101B-9F08-00AA002F954E}"
        };
        LayoutDocument source = Document(desktopItem, namespaceItem);

        StartupCustodyReconciliation result = StartupCustodyReconciler.Reconcile(
            source,
            ItemsRoot,
            [DesktopRoot],
            _ => Path.Combine(DesktopRoot, desktopItem.Name),
            _ => StartupPathState.Missing,
            _ => true);

        result.RemovedItemIds.Should().BeEmpty();
        result.Document.Should().BeSameAs(source);
        result.Document.Fences[0].Items.Should().HaveCount(2);
    }

    [Fact]
    public void Reconcile_PreservesReferenceWhenPathStateIsUnavailable()
    {
        FenceItemState item = Stored("temporariamente-inacessivel.txt");
        LayoutDocument source = Document(item);
        string stablePath = FenceItemStore.PayloadPath(ItemsRoot, item.ItemId, item.StorageName!);

        StartupCustodyReconciliation result = Reconcile(
            source,
            path => path == stablePath ? StartupPathState.Unavailable : StartupPathState.Missing);

        result.RemovedItemIds.Should().BeEmpty();
        result.Document.Fences[0].Items.Should().ContainSingle(item => item.ItemId == source.Fences[0].Items[0].ItemId);
    }

    [Fact]
    public void Reconcile_DoesNotClassifyExternalPathAsDeletedDesktopItem()
    {
        FenceItemState item = Stored("externo.txt");
        item.OriginalPath = @"C:\Arquivos\externo.txt";
        LayoutDocument source = Document(item);

        StartupCustodyReconciliation result = Reconcile(
            source,
            _ => StartupPathState.Missing);

        result.RemovedItemIds.Should().BeEmpty();
        result.Document.Should().BeSameAs(source);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Reconcile_PreservesReferenceWhenStoreOrDesktopCannotBeInspected(
        bool storeReadable,
        bool desktopReadable)
    {
        FenceItemState item = Stored("ambiguo.txt");
        LayoutDocument source = Document(item);

        StartupCustodyReconciliation result = StartupCustodyReconciler.Reconcile(
            source,
            ItemsRoot,
            [DesktopRoot],
            _ => null,
            _ => StartupPathState.Missing,
            path => path == ItemsRoot ? storeReadable : desktopReadable);

        result.RemovedItemIds.Should().BeEmpty();
        result.Document.Should().BeSameAs(source);
    }

    private static StartupCustodyReconciliation Reconcile(
        LayoutDocument source,
        Func<string, StartupPathState> probePath) =>
        StartupCustodyReconciler.Reconcile(
            source,
            ItemsRoot,
            [DesktopRoot],
            _ => null,
            probePath,
            _ => true);

    private static LayoutDocument Document(params FenceItemState[] items) => new()
    {
        Revision = 7,
        Fences =
        [
            new FenceState
            {
                Id = Guid.NewGuid(),
                Title = "Teste",
                X = 10,
                Y = 20,
                Items = [.. items]
            }
        ]
    };

    private static FenceItemState Stored(string name) => new()
    {
        ItemId = Guid.NewGuid(),
        Kind = FenceItemKind.Stored,
        Name = name,
        StorageName = name,
        OriginalPath = Path.Combine(DesktopRoot, name),
        OriginalX = 100,
        OriginalY = 200
    };
}
