using DesktopFences.Core;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class FenceItemStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "df-store-" + Guid.NewGuid().ToString("N"));

    public FenceItemStoreTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public void UniqueDestination_KeepsNameWhenFree()
    {
        string dest = FenceItemStore.UniqueDestination(_dir, "VS Code.lnk");
        dest.Should().Be(Path.Combine(_dir, "VS Code.lnk"));
    }

    [Fact]
    public void UniqueDestination_SuffixesWhenTaken()
    {
        File.WriteAllText(Path.Combine(_dir, "VS Code.lnk"), "a");
        string dest = FenceItemStore.UniqueDestination(_dir, "VS Code.lnk");
        dest.Should().Be(Path.Combine(_dir, "VS Code (2).lnk"));
    }

    [Fact]
    public void FolderForItem_UsesStableItemGuid()
    {
        var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        FenceItemStore.FolderForItem(id).Should().EndWith(Path.Combine("DesktopFences", "Items", id.ToString("D")));
    }

    [Fact]
    public void PayloadPath_RejectsTraversal()
    {
        Action act = () => FenceItemStore.PayloadPath(_dir, Guid.NewGuid(), @"..\escape.txt");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RestoreDirectory_PrefersOriginalParentWhenItExists()
    {
        string original = Path.Combine(_dir, "Chrome.lnk");
        FenceItemStore.RestoreDirectory(original, [@"C:\Users\Test\Desktop"]).Should().Be(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
