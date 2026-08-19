using DesktopFences.Core.Fences;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class ShellNewMenuCacheTests
{
    [Fact]
    public void WithExtension_AppendsWhenMissing()
    {
        IReadOnlyList<string> existing = [".lnk", "Folder", ".zip"];

        IReadOnlyList<string> next = ShellNewMenuCache.WithExtension(existing, ".desktopfence");

        next.Should().Equal(".lnk", "Folder", ".zip", ".desktopfence");
    }

    [Fact]
    public void WithExtension_IsIdempotent()
    {
        IReadOnlyList<string> existing = [".lnk", ".desktopfence", "Folder"];

        ShellNewMenuCache.WithExtension(existing, ".DESKTOPFENCE").Should().BeSameAs(existing);
    }

    [Fact]
    public void WithoutExtension_RemovesOnlyTheStubType()
    {
        IReadOnlyList<string> existing = [".lnk", ".desktopfence", "Folder"];

        ShellNewMenuCache.WithoutExtension(existing, ".desktopfence")
            .Should().Equal(".lnk", "Folder");
    }

    [Fact]
    public void WithoutExtension_LeavesListWhenAbsent()
    {
        IReadOnlyList<string> existing = [".lnk", "Folder"];

        ShellNewMenuCache.WithoutExtension(existing, ".desktopfence").Should().BeSameAs(existing);
    }
}
