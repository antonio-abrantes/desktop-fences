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
    public void RestoreDirectory_PreservesOriginalUserDesktop()
    {
        string userDesktop = Directory.CreateDirectory(Path.Combine(_dir, "UserDesktop")).FullName;
        string publicDesktop = Directory.CreateDirectory(Path.Combine(_dir, "PublicDesktop")).FullName;
        string original = Path.Combine(userDesktop, "Chrome.lnk");

        FenceItemStore.RestoreDirectory(original, [userDesktop, publicDesktop])
            .Should().Be(userDesktop);
    }

    [Fact]
    public void RestoreDirectory_RedirectsPublicDesktopToUserDesktop()
    {
        string userDesktop = Directory.CreateDirectory(Path.Combine(_dir, "UserDesktop")).FullName;
        string publicDesktop = Directory.CreateDirectory(Path.Combine(_dir, "PublicDesktop")).FullName;
        string original = Path.Combine(publicDesktop, "AtalhoPublico.lnk");

        FenceItemStore.RestoreDirectory(original, [userDesktop, publicDesktop])
            .Should().Be(userDesktop);
    }

    [Fact]
    public void RestoreDirectory_DoesNotRestoreOutsideConfiguredDesktop()
    {
        string userDesktop = Directory.CreateDirectory(Path.Combine(_dir, "UserDesktop")).FullName;
        string publicDesktop = Directory.CreateDirectory(Path.Combine(_dir, "PublicDesktop")).FullName;
        string external = Directory.CreateDirectory(Path.Combine(_dir, "External")).FullName;
        string original = Path.Combine(external, "Arquivo.txt");

        FenceItemStore.RestoreDirectory(original, [userDesktop, publicDesktop])
            .Should().Be(userDesktop);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
