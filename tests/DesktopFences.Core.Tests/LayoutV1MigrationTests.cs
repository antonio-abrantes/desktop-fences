using DesktopFences.Core.Models;
using DesktopFences.Core.Persistence;
using DesktopFences.Core.Transactions;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class LayoutV1MigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "df-migration-" + Guid.NewGuid().ToString("N"));
    private string ItemsRoot => Path.Combine(_root, "Items");

    public LayoutV1MigrationTests() => Directory.CreateDirectory(ItemsRoot);

    [Fact]
    public void Plan_MovesLegacyFencePayloadToStableItemFolder()
    {
        Guid fenceId = Guid.NewGuid();
        string legacyFolder = Path.Combine(ItemsRoot, fenceId.ToString("D"));
        Directory.CreateDirectory(legacyFolder);
        string legacyPath = Path.Combine(legacyFolder, "arquivo.txt");
        File.WriteAllText(legacyPath, "payload");

        LayoutMigrationPlan plan = LayoutV1Migration.Plan(Legacy(fenceId, legacyPath), ItemsRoot);

        FenceItemState item = plan.Document.Fences.Single().Items.Single();
        plan.Document.Version.Should().Be(2);
        item.ItemId.Should().NotBe(Guid.Empty);
        item.Kind.Should().Be(FenceItemKind.Stored);
        item.StorageName.Should().Be("arquivo.txt");
        item.Path.Should().BeNull();
        plan.Transaction.Should().NotBeNull();
        CustodyTransactionItem move = plan.Transaction!.Items.Single();
        move.SourcePath.Should().Be(legacyPath);
        move.DestinationPath.Should().Be(Path.Combine(ItemsRoot, item.ItemId.ToString("D"), "arquivo.txt"));
    }

    [Fact]
    public void Plan_HandlesPayloadAlreadyRestoredToDesktopWithoutPhysicalMigration()
    {
        Guid fenceId = Guid.NewGuid();
        string desktop = Path.Combine(_root, "Desktop");
        Directory.CreateDirectory(desktop);
        string path = Path.Combine(desktop, "arquivo.txt");
        File.WriteAllText(path, "payload");

        LayoutMigrationPlan plan = LayoutV1Migration.Plan(Legacy(fenceId, path), ItemsRoot);

        FenceItemState item = plan.Document.Fences.Single().Items.Single();
        item.Kind.Should().Be(FenceItemKind.Stored);
        item.OriginalPath.Should().Be(path);
        item.StorageName.Should().Be("arquivo.txt");
        plan.Transaction.Should().BeNull();
    }

    [Fact]
    public void Plan_HandlesLegacyStorePathWhenPayloadWasRestoredToOriginalDesktopPath()
    {
        Guid fenceId = Guid.NewGuid();
        string legacyPath = Path.Combine(ItemsRoot, fenceId.ToString("D"), "atalho.lnk");
        string desktopPath = Path.Combine(_root, "Desktop", "atalho.lnk");
        Directory.CreateDirectory(Path.GetDirectoryName(desktopPath)!);
        File.WriteAllText(desktopPath, "shortcut");
        LayoutDocument legacy = Legacy(fenceId, legacyPath);
        legacy.Fences[0].Items[0].OriginalPath = desktopPath;

        LayoutMigrationPlan plan = LayoutV1Migration.Plan(legacy, ItemsRoot);

        FenceItemState item = plan.Document.Fences[0].Items.Single();
        item.Kind.Should().Be(FenceItemKind.Stored);
        item.StorageName.Should().Be("atalho.lnk");
        item.OriginalPath.Should().Be(desktopPath);
        item.Path.Should().BeNull();
        plan.Transaction.Should().BeNull();
    }

    [Fact]
    public void Plan_HandlesMixedLegacyStateWithStoredAndAlreadyRestoredPayloads()
    {
        Guid fenceId = Guid.NewGuid();
        string legacyFolder = Path.Combine(ItemsRoot, fenceId.ToString("D"));
        Directory.CreateDirectory(legacyFolder);
        string storedPath = Path.Combine(legacyFolder, "guardado.lnk");
        File.WriteAllText(storedPath, "stored");
        string staleStorePath = Path.Combine(legacyFolder, "restaurado.lnk");
        string desktopPath = Path.Combine(_root, "Desktop", "restaurado.lnk");
        Directory.CreateDirectory(Path.GetDirectoryName(desktopPath)!);
        File.WriteAllText(desktopPath, "restored");

        LayoutDocument legacy = Legacy(fenceId, storedPath);
        legacy.Fences[0].Items.Add(new FenceItemState
        {
            Name = "restaurado",
            Path = staleStorePath,
            OriginalPath = desktopPath
        });

        LayoutMigrationPlan plan = LayoutV1Migration.Plan(legacy, ItemsRoot);

        plan.Document.Fences[0].Items.Should().HaveCount(2)
            .And.OnlyContain(item => item.Kind == FenceItemKind.Stored && item.ItemId != Guid.Empty);
        plan.Transaction.Should().NotBeNull();
        plan.Transaction!.Items.Should().ContainSingle()
            .Which.SourcePath.Should().Be(storedPath);
        plan.Document.Fences[0].Items.Single(item => item.Name == "restaurado")
            .StorageName.Should().Be("restaurado.lnk");
    }

    [Fact]
    public void Plan_StopsOnPayloadInsideAnotherFenceFolder()
    {
        Guid fenceId = Guid.NewGuid();
        string other = Path.Combine(ItemsRoot, Guid.NewGuid().ToString("D"));
        Directory.CreateDirectory(other);
        string path = Path.Combine(other, "arquivo.txt");
        File.WriteAllText(path, "payload");

        Action act = () => LayoutV1Migration.Plan(Legacy(fenceId, path), ItemsRoot);

        act.Should().Throw<InvalidDataException>().WithMessage("*ambíguo*");
    }

    [Fact]
    public void Plan_StopsWhenReferencedLegacyPayloadIsMissing()
    {
        Guid fenceId = Guid.NewGuid();
        string path = Path.Combine(ItemsRoot, fenceId.ToString("D"), "ausente.txt");

        Action act = () => LayoutV1Migration.Plan(Legacy(fenceId, path), ItemsRoot);

        act.Should().Throw<InvalidDataException>().WithMessage("*ausente*");
    }

    [Theory]
    [InlineData("atalho.lnk", false)]
    [InlineData("site.url", false)]
    [InlineData("Pasta", true)]
    public void Plan_MigratesSupportedPayloadKinds(string name, bool directory)
    {
        Guid fenceId = Guid.NewGuid();
        string legacyFolder = Path.Combine(ItemsRoot, fenceId.ToString("D"));
        Directory.CreateDirectory(legacyFolder);
        string path = Path.Combine(legacyFolder, name);
        if (directory)
            Directory.CreateDirectory(path);
        else
            File.WriteAllText(path, "payload");

        LayoutMigrationPlan plan = LayoutV1Migration.Plan(Legacy(fenceId, path), ItemsRoot);

        plan.Document.Fences.Single().Items.Single().StorageName.Should().Be(name);
        plan.Transaction!.Items.Should().ContainSingle();
    }

    [Fact]
    public void Plan_PreservesNamespaceWithoutCreatingPayloadMove()
    {
        LayoutDocument legacy = Legacy(Guid.NewGuid(), "::{645FF040-5081-101B-9F08-00AA002F954E}");

        LayoutMigrationPlan plan = LayoutV1Migration.Plan(legacy, ItemsRoot);

        FenceItemState item = plan.Document.Fences.Single().Items.Single();
        item.Kind.Should().Be(FenceItemKind.Namespace);
        item.StorageName.Should().BeNull();
        plan.Transaction.Should().BeNull();
    }

    [Fact]
    public void Plan_SamePayloadNameInDifferentFencesGetsDifferentItemStores()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        string firstPath = CreateLegacyFile(first, "igual.txt");
        string secondPath = CreateLegacyFile(second, "igual.txt");
        LayoutDocument legacy = new()
        {
            Version = 1,
            Fences =
            [
                new FenceState { Id = first, Items = [new FenceItemState { Name = "igual.txt", Path = firstPath }] },
                new FenceState { Id = second, Items = [new FenceItemState { Name = "igual.txt", Path = secondPath }] }
            ]
        };

        LayoutMigrationPlan plan = LayoutV1Migration.Plan(legacy, ItemsRoot);

        plan.Transaction!.Items.Select(i => i.DestinationPath).Should().OnlyHaveUniqueItems();
    }

    private static LayoutDocument Legacy(Guid fenceId, string path) => new()
    {
        Version = 1,
        Fences =
        [
            new FenceState
            {
                Id = fenceId,
                Items = [new FenceItemState { Name = "arquivo.txt", Path = path }]
            }
        ]
    };

    private string CreateLegacyFile(Guid fenceId, string name)
    {
        string folder = Path.Combine(ItemsRoot, fenceId.ToString("D"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, name);
        File.WriteAllText(path, "payload");
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }
}
