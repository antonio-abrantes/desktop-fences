using DesktopFences.Core.Fences;
using DesktopFences.Core.Models;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class FenceLayoutRulesTests
{
    [Fact]
    public void EnsureAtLeastOne_AddsDefault_WhenEmpty()
    {
        List<FenceState> fences = [];
        FenceLayoutRules.EnsureAtLeastOne(fences);
        fences.Should().ContainSingle();
        fences[0].Title.Should().Be("Nova fence");
        fences[0].TitleAlignment.Should().Be(TitleAlignment.Left);
        fences[0].Width.Should().Be(FenceLayoutRules.DefaultWidth);
    }

    [Fact]
    public void EnsureAtLeastOne_LeavesExisting()
    {
        var id = Guid.NewGuid();
        List<FenceState> fences = [new FenceState { Id = id, Title = "Trabalho" }];
        FenceLayoutRules.EnsureAtLeastOne(fences);
        fences.Should().ContainSingle().Which.Id.Should().Be(id);
    }

    [Fact]
    public void CanRemove_IsFalse_WhenOnlyOne()
    {
        FenceLayoutRules.CanRemove(1).Should().BeFalse();
        FenceLayoutRules.CanRemove(0).Should().BeFalse();
        FenceLayoutRules.CanRemove(2).Should().BeTrue();
    }

    [Fact]
    public void PlaceNew_OffsetsFromLastFence()
    {
        var existing = new List<FenceState>
        {
            new() { X = 100, Y = 50, Width = 380, Height = 280 }
        };

        FenceState created = FenceLayoutRules.PlaceNew(existing);
        created.Id.Should().NotBe(existing[0].Id);
        created.Title.Should().Be("Nova fence");
        created.TitleAlignment.Should().Be(TitleAlignment.Left);
        created.X.Should().Be(100 + FenceLayoutRules.PlaceOffset);
        created.Y.Should().Be(50 + FenceLayoutRules.PlaceOffset);
        created.Width.Should().Be(380);
        created.Height.Should().Be(280);
    }

    [Fact]
    public void PlaceNew_UsesDefault_WhenListEmpty()
    {
        FenceState created = FenceLayoutRules.PlaceNew([]);
        created.X.Should().Be(FenceLayoutRules.DefaultX + FenceLayoutRules.PlaceOffset);
        created.Y.Should().Be(FenceLayoutRules.DefaultY + FenceLayoutRules.PlaceOffset);
    }
}
