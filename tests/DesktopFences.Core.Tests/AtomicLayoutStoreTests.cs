using DesktopFences.Core.Models;
using DesktopFences.Core.Persistence;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class AtomicLayoutStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "df-atomic-" + Guid.NewGuid().ToString("N"));
    private readonly string _file;

    public AtomicLayoutStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "layout.json");
    }

    [Fact]
    public void Save_ReplacesPrimaryAndKeepsPreviousVersionAsBackup()
    {
        var store = new LayoutStore(_file);
        store.Save(Document("Primeiro", revision: 1));
        store.Save(Document("Segundo", revision: 2));

        store.LoadOrEmpty().Fences.Single().Title.Should().Be("Segundo");
        File.Exists(store.BackupPath).Should().BeTrue();
        File.ReadAllText(store.BackupPath).Should().Contain("Primeiro");
        File.Exists(store.TempPath).Should().BeFalse();
    }

    [Fact]
    public void LoadOrEmpty_UsesBackupWhenPrimaryIsCorrupt()
    {
        var store = new LayoutStore(_file);
        store.Save(Document("Seguro", revision: 1));
        store.Save(Document("Atual", revision: 2));
        File.WriteAllText(_file, "{quebrado");

        LayoutDocument loaded = store.LoadOrEmpty();

        loaded.Fences.Single().Title.Should().Be("Seguro");
        loaded.Revision.Should().Be(1);
    }

    [Fact]
    public void LoadOrEmpty_UsesBackupWhenPrimaryIsSyntacticallyValidButViolatesV2Invariants()
    {
        var store = new LayoutStore(_file);
        store.Save(Document("Seguro", revision: 1));
        store.Save(Document("Atual", revision: 2));
        File.WriteAllText(_file, """
            {"version":2,"revision":3,"fences":[{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","items":[{"itemId":"00000000-0000-0000-0000-000000000000","kind":"stored","name":"x","storageName":"x"}]}]}
            """);

        LayoutDocument loaded = store.LoadOrEmpty();

        loaded.Fences.Single().Title.Should().Be("Seguro");
    }

    [Fact]
    public void LoadOrEmpty_PrefersV2BackupOverDowngradedV1Primary()
    {
        var store = new LayoutStore(_file);
        store.Save(Document("Seguro v2", revision: 88));
        File.Copy(_file, store.BackupPath, overwrite: true);
        File.WriteAllText(_file, """
            {"version":1,"revision":0,"fences":[{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","title":"Regredido"}]}
            """);

        LayoutDocument loaded = store.LoadOrEmpty();

        loaded.Version.Should().Be(2);
        loaded.Revision.Should().Be(88);
        loaded.Fences.Single().Title.Should().Be("Seguro v2");
    }

    [Fact]
    public void LoadOrEmpty_DoesNotCreateEmptyLayoutWhenPrimaryAndBackupAreInvalid()
    {
        var store = new LayoutStore(_file);
        File.WriteAllText(_file, "x");
        File.WriteAllText(store.BackupPath, "y");

        Action act = () => store.LoadOrEmpty();

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Save_RejectsDuplicateItemIdsBeforeTouchingPrimary()
    {
        var store = new LayoutStore(_file);
        store.Save(Document("Válido", revision: 1));
        string original = File.ReadAllText(_file);
        Guid duplicate = Guid.NewGuid();
        LayoutDocument invalid = Document("Inválido", revision: 2);
        invalid.Fences.Single().Items = [Item("a.txt", duplicate), Item("b.txt", duplicate)];

        Action act = () => store.Save(invalid);

        act.Should().Throw<InvalidDataException>();
        File.ReadAllText(_file).Should().Be(original);
    }

    [Fact]
    public void LoadOrEmpty_IgnoresTruncatedTemporaryFileAndKeepsPrimary()
    {
        var store = new LayoutStore(_file);
        store.Save(Document("Principal", revision: 1));
        File.WriteAllText(store.TempPath, "{truncado");

        LayoutDocument loaded = store.LoadOrEmpty();

        loaded.Fences.Single().Title.Should().Be("Principal");
    }

    private static LayoutDocument Document(string title, long revision) => new()
    {
        Revision = revision,
        Fences = [new FenceState { Id = Guid.NewGuid(), Title = title, Items = [Item("a.txt", Guid.NewGuid())] }]
    };

    private static FenceItemState Item(string name, Guid id) => new()
    {
        ItemId = id,
        Kind = FenceItemKind.Stored,
        Name = name,
        StorageName = name
    };

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
