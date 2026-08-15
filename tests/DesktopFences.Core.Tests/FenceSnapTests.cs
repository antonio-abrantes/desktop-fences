using DesktopFences.Core.Fences;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class FenceSnapTests
{
    private static readonly SnapRect Work = new(0, 0, 1920, 1080);
    private static readonly SnapRect Fence = new(80, 80, 380, 280);

    [Fact]
    public void Translate_NearWorkLeft_SnapsFlush()
    {
        SnapRect moving = Fence with { X = 8 };
        FenceSnap.Translate(moving, Work, []).X.Should().Be(0);
    }

    [Fact]
    public void Translate_FarFromEdges_Stays()
    {
        FenceSnap.Translate(Fence, Work, []).Should().Be(Fence);
    }

    [Fact]
    public void Translate_AtThreshold_Snaps()
    {
        SnapRect moving = Fence with { X = FenceSnap.DefaultThreshold };
        FenceSnap.Translate(moving, Work, []).X.Should().Be(0);
    }

    [Fact]
    public void Translate_PastThreshold_DoesNotSnap()
    {
        SnapRect moving = Fence with { X = FenceSnap.DefaultThreshold + 1 };
        FenceSnap.Translate(moving, Work, []).X.Should().Be(moving.X);
    }

    [Fact]
    public void Translate_NearWorkRight_SnapsFlush()
    {
        SnapRect moving = Fence with { X = 1920 - 380 - 6 };
        FenceSnap.Translate(moving, Work, []).Right.Should().Be(1920);
    }

    [Fact]
    public void Translate_NearWorkTopAndLeft_SnapsBothAxes()
    {
        SnapRect moving = new(7, 9, 380, 280);
        SnapRect snapped = FenceSnap.Translate(moving, Work, []);
        snapped.X.Should().Be(0);
        snapped.Y.Should().Be(0);
        snapped.Width.Should().Be(380);
        snapped.Height.Should().Be(280);
    }

    [Fact]
    public void Translate_AbutsOtherFenceRight()
    {
        SnapRect other = new(0, 40, 200, 160);
        SnapRect moving = Fence with { X = 208 };
        FenceSnap.Translate(moving, Work, [other]).X.Should().Be(200);
    }

    [Fact]
    public void Translate_AlignsLeftEdges()
    {
        SnapRect other = new(400, 40, 200, 160);
        SnapRect moving = Fence with { X = 408 };
        FenceSnap.Translate(moving, Work, [other]).X.Should().Be(400);
    }

    [Fact]
    public void Translate_PicksClosestCandidate()
    {
        SnapRect other = new(10, 40, 200, 160);
        SnapRect moving = Fence with { X = 4 };
        // work left delta = -4; other left delta = 6. Closest is work.
        FenceSnap.Translate(moving, Work, [other]).X.Should().Be(0);
    }

    [Fact]
    public void Translate_DoesNotChangeOtherRects()
    {
        var others = new List<SnapRect> { new(0, 0, 200, 100) };
        SnapRect moving = Fence with { X = 208 };
        FenceSnap.Translate(moving, Work, others);
        others[0].Should().Be(new SnapRect(0, 0, 200, 100));
    }

    [Fact]
    public void Translate_OverlappingCentersWithoutNearbyEdges_DoesNotSnap()
    {
        SnapRect other = new(200, 200, 380, 280);
        SnapRect moving = new(260, 260, 380, 280);
        FenceSnap.Translate(moving, Work, [other]).Should().Be(moving);
    }

    [Fact]
    public void Edges_NearWorkRight_GrowsWidth()
    {
        SnapRect moving = new(1500, 80, 414, 280);
        SnapRect snapped = FenceSnap.Edges(moving, Work, [], minWidth: 180, minHeight: 80);
        snapped.X.Should().Be(1500);
        snapped.Right.Should().Be(1920);
    }

    [Fact]
    public void Edges_NearOtherLeft_ShrinksToAbut()
    {
        SnapRect other = new(600, 40, 200, 160);
        SnapRect moving = new(80, 80, 528, 280);
        SnapRect snapped = FenceSnap.Edges(moving, Work, [other], minWidth: 180, minHeight: 80);
        snapped.X.Should().Be(80);
        snapped.Right.Should().Be(600);
    }

    [Fact]
    public void Edges_ConflictingSnapsBelowMinWidth_KeepsCloserEdge()
    {
        SnapRect moving = new(8, 80, 180, 280);
        SnapRect snapped = FenceSnap.Edges(
            moving,
            new SnapRect(0, 0, 170, 1080),
            [],
            minWidth: 180,
            minHeight: 80);
        snapped.Width.Should().BeGreaterThanOrEqualTo(180);
        snapped.X.Should().Be(0);
        snapped.Right.Should().Be(188);
    }

    [Fact]
    public void Edges_NearWorkBottom_GrowsHeight()
    {
        SnapRect moving = new(80, 790, 380, 284);
        SnapRect snapped = FenceSnap.Edges(moving, Work, [], minWidth: 180, minHeight: 80);
        snapped.Y.Should().Be(790);
        snapped.Bottom.Should().Be(1080);
    }

    [Fact]
    public void Edges_NearOtherTop_ShrinksToAbut()
    {
        SnapRect other = new(40, 500, 200, 160);
        SnapRect moving = new(80, 80, 380, 428);
        SnapRect snapped = FenceSnap.Edges(moving, Work, [other], minWidth: 180, minHeight: 80);
        snapped.Y.Should().Be(80);
        snapped.Bottom.Should().Be(500);
    }

    [Fact]
    public void Translate_RejectsNegativeThreshold()
    {
        Action act = () => FenceSnap.Translate(Fence, Work, [], threshold: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
