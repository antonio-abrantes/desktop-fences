using System.IO;
using DesktopFences.App;
using DesktopFences.Core.Models;
using DesktopFences.Core.Persistence;
using FluentAssertions;
using Xunit;

namespace DesktopFences.App.Tests;

public sealed class InstallerDataPolicyTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "DesktopFences-InstallerTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SetLanguagePreservesExistingFencesAndItems()
    {
        InstallerDataPaths paths = Paths();
        var original = new LayoutDocument
        {
            Revision = 7,
            UiLanguage = "pt",
            Fences =
            [
                new FenceState
                {
                    Title = "Trabalho",
                    Items =
                    [
                        new FenceItemState
                        {
                            ItemId = Guid.NewGuid(),
                            Name = "a.txt",
                            StorageName = "a.txt",
                            OriginalPath = Path.Combine(_root, "Desktop", "a.txt")
                        }
                    ]
                }
            ]
        };
        new LayoutStore(paths.LayoutPath).Save(original);

        new InstallerDataPolicy(paths).SetLanguage("en");

        LayoutDocument saved = new LayoutStore(paths.LayoutPath).LoadOrEmpty();
        saved.UiLanguage.Should().Be("en");
        saved.Revision.Should().Be(8);
        saved.Fences.Should().ContainSingle().Which.Title.Should().Be("Trabalho");
        saved.Fences[0].Items.Should().ContainSingle().Which.Name.Should().Be("a.txt");
    }

    [Fact]
    public void ResetArchivesOldStateAndCreatesCleanLanguagePreference()
    {
        InstallerDataPaths paths = Paths();
        Directory.CreateDirectory(paths.RoamingRoot);
        Directory.CreateDirectory(paths.LocalRoot);
        File.WriteAllText(Path.Combine(paths.RoamingRoot, "old.txt"), "roaming");
        File.WriteAllText(Path.Combine(paths.LocalRoot, "old.txt"), "local");

        string archive = new InstallerDataPolicy(paths).ResetAfterRelease("en");

        File.Exists(Path.Combine(paths.RoamingRoot, "old.txt")).Should().BeFalse();
        Directory.Exists(paths.LocalRoot).Should().BeFalse();
        File.ReadAllText(Path.Combine(archive, "Roaming", "old.txt")).Should().Be("roaming");
        File.ReadAllText(Path.Combine(archive, "Local", "old.txt")).Should().Be("local");
        LayoutDocument fresh = new LayoutStore(paths.LayoutPath).LoadOrEmpty();
        fresh.UiLanguage.Should().Be("en");
        fresh.Fences.Should().BeEmpty();
    }

    [Fact]
    public void RemoveDeletesOnlyTheConfiguredDataRoots()
    {
        InstallerDataPaths paths = Paths();
        Directory.CreateDirectory(paths.RoamingRoot);
        Directory.CreateDirectory(paths.LocalRoot);
        string outside = Path.Combine(_root, "outside.txt");
        File.WriteAllText(outside, "keep");

        new InstallerDataPolicy(paths).RemoveAfterRelease();

        Directory.Exists(paths.RoamingRoot).Should().BeFalse();
        Directory.Exists(paths.LocalRoot).Should().BeFalse();
        File.ReadAllText(outside).Should().Be("keep");
    }

    private InstallerDataPaths Paths()
    {
        string roaming = Path.Combine(_root, "Roaming", "DesktopFences");
        string local = Path.Combine(_root, "Local", "DesktopFences");
        return new InstallerDataPaths(
            Path.Combine(roaming, "layout.json"),
            roaming,
            local,
            Path.Combine(_root, "Backups"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
