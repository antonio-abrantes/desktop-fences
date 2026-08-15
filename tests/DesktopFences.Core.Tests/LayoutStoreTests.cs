using DesktopFences.Core.Models;
using DesktopFences.Core.Persistence;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class LayoutStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "DesktopFencesTests", Guid.NewGuid().ToString("N"));
    private readonly string _file;

    public LayoutStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "layout.json");
    }

    [Fact]
    public void LoadOrEmpty_ReturnsEmpty_WhenFileMissing()
    {
        var store = new LayoutStore(_file);
        store.LoadOrEmpty().Fences.Should().BeEmpty();
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsFenceGeometry()
    {
        var store = new LayoutStore(_file);
        var id = Guid.NewGuid();
        store.Save(new LayoutDocument
        {
            Fences =
            [
                new FenceState
                {
                    Id = id,
                    Title = "Trabalho",
                    X = 40,
                    Y = 80,
                    Width = 400,
                    Height = 240,
                    Items = [new FenceItemState { Name = "a.txt", OriginalX = 10, OriginalY = 20 }]
                }
            ]
        });

        LayoutDocument loaded = store.LoadOrEmpty();
        loaded.Version.Should().Be(1);
        loaded.Fences.Should().ContainSingle();
        FenceState fence = loaded.Fences[0];
        fence.Id.Should().Be(id);
        fence.Title.Should().Be("Trabalho");
        fence.X.Should().Be(40);
        fence.Items.Should().ContainSingle().Which.Name.Should().Be("a.txt");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
