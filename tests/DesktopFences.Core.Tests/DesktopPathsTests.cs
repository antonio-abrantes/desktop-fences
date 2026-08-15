using DesktopFences.Core;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class DesktopPathsTests
{
    [Fact]
    public void ResolveExisting_PrefersShortcutOverFolderWithTheSameVisibleName()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop))
            return;

        string folder = Path.Combine(desktop, "__df_folder_vs_lnk__");
        string lnk = Path.Combine(desktop, "__df_folder_vs_lnk__.lnk");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(lnk, "dummy");
            string? resolved = DesktopPaths.ResolveExisting("__df_folder_vs_lnk__");
            resolved.Should().Be(lnk);
        }
        finally
        {
            try { File.Delete(lnk); } catch { }
            try { Directory.Delete(folder); } catch { }
        }
    }

    [Fact]
    public void ResolveExisting_PrefersSiblingShortcutWhenGivenARootedFolderPath()
    {
        string root = Path.Combine(Path.GetTempPath(), "__df_rooted_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string folder = Path.Combine(root, "AppName");
        string lnk = Path.Combine(root, "AppName.lnk");
        Directory.CreateDirectory(folder);
        File.WriteAllText(lnk, "dummy");
        try
        {
            DesktopPaths.ResolveExisting(folder).Should().Be(lnk);
        }
        finally
        {
            try { File.Delete(lnk); } catch { }
            try { Directory.Delete(folder); } catch { }
            try { Directory.Delete(root); } catch { }
        }
    }

    [Fact]
    public void VisibleName_StripsShortcutExtensionOnly()
    {
        DesktopPaths.VisibleName(@"C:\Desktop\Chrome.lnk").Should().Be("Chrome");
        DesktopPaths.VisibleName("notes.txt").Should().Be("notes.txt");
        DesktopPaths.VisibleName("Pasta").Should().Be("Pasta");
    }
}
