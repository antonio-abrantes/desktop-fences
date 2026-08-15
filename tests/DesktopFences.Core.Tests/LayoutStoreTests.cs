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
        fence.TitleAlignment.Should().Be(TitleAlignment.Left);
        fence.Theme.Should().BeNull();
        fence.X.Should().Be(40);
        fence.Items.Should().ContainSingle().Which.Name.Should().Be("a.txt");
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsTitleAlignmentCenter()
    {
        var store = new LayoutStore(_file);
        store.Save(new LayoutDocument
        {
            Fences = [new FenceState { Title = "Jogos", TitleAlignment = TitleAlignment.Center }]
        });

        string json = File.ReadAllText(_file);
        json.Should().Contain("\"titleAlignment\": \"center\"");
        store.LoadOrEmpty().Fences.Should().ContainSingle().Which.TitleAlignment.Should().Be(TitleAlignment.Center);
    }

    [Fact]
    public void LoadOrEmpty_DefaultsTitleAlignmentLeft_WhenFieldMissing()
    {
        File.WriteAllText(_file, """
            {"version":1,"fences":[{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","title":"Old"}]}
            """);

        FenceState fence = new LayoutStore(_file).LoadOrEmpty().Fences.Should().ContainSingle().Subject;
        fence.Title.Should().Be("Old");
        fence.TitleAlignment.Should().Be(TitleAlignment.Left);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsTheme()
    {
        var store = new LayoutStore(_file);
        store.Save(new LayoutDocument
        {
            Fences =
            [
                new FenceState
                {
                    Title = "Glass",
                    Theme = new FenceTheme
                    {
                        Fill = "#A83264C8",
                        Border = "#80FF8800",
                        Header = "#40000000",
                        Text = "#F2FFEEDD"
                    }
                }
            ]
        });

        FenceTheme? theme = new LayoutStore(_file).LoadOrEmpty().Fences.Should().ContainSingle().Subject.Theme;
        theme.Should().NotBeNull();
        theme!.Fill.Should().Be("#A83264C8");
        theme.Border.Should().Be("#80FF8800");
        theme.Header.Should().Be("#40000000");
        theme.Text.Should().Be("#F2FFEEDD");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
