using DesktopFences.Core.Occupancy;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class FenceItemDropTests
{
    private static readonly Guid SourceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ThirdId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static FenceScreenTarget Expanded(Guid id, int x, int y, int width = 200, int height = 160, int title = 36)
        => new(
            id,
            WindowX: x,
            WindowY: y,
            WindowWidth: width,
            WindowHeight: height,
            BodyX: x,
            BodyY: y + title,
            BodyWidth: width,
            BodyHeight: height - title,
            Collapsed: false);

    private static FenceScreenTarget Collapsed(Guid id, int x, int y, int width = 200, int title = 36)
        => new(
            id,
            WindowX: x,
            WindowY: y,
            WindowWidth: width,
            WindowHeight: title,
            BodyX: x,
            BodyY: y + title,
            BodyWidth: 0,
            BodyHeight: 0,
            Collapsed: true);

    [Fact]
    public void Resolve_PointInSourceBody_Stays()
    {
        FenceScreenTarget source = Expanded(SourceId, 0, 0);
        FenceItemDrop.Resolve([source], SourceId, 20, 80).Should().Be(FenceItemDropKind.Stay);
        FenceItemDrop.TransferTargetId([source], SourceId, 20, 80).Should().BeNull();
    }

    [Fact]
    public void Resolve_PointInOtherBody_Transfers()
    {
        var fences = new[] { Expanded(SourceId, 0, 0), Expanded(OtherId, 400, 0) };
        FenceItemDrop.Resolve(fences, SourceId, 420, 80).Should().Be(FenceItemDropKind.Transfer);
        FenceItemDrop.TransferTargetId(fences, SourceId, 420, 80).Should().Be(OtherId);
    }

    [Fact]
    public void Resolve_PointOnDesktop_Ejects()
    {
        var fences = new[] { Expanded(SourceId, 0, 0), Expanded(OtherId, 400, 0) };
        FenceItemDrop.Resolve(fences, SourceId, 900, 900).Should().Be(FenceItemDropKind.Eject);
        FenceItemDrop.TransferTargetId(fences, SourceId, 900, 900).Should().BeNull();
    }

    [Fact]
    public void Resolve_PointOnOtherTitleBar_DoesNotEjectOrTransfer()
    {
        var fences = new[] { Expanded(SourceId, 0, 0), Expanded(OtherId, 400, 0) };
        FenceItemDrop.Resolve(fences, SourceId, 420, 10).Should().Be(FenceItemDropKind.Stay);
        FenceItemDrop.TransferTargetId(fences, SourceId, 420, 10).Should().BeNull();
    }

    [Fact]
    public void Resolve_PointOnCollapsedOtherFence_DoesNotTransfer()
    {
        var fences = new[] { Expanded(SourceId, 0, 0), Collapsed(OtherId, 400, 0) };
        FenceItemDrop.Resolve(fences, SourceId, 420, 10).Should().Be(FenceItemDropKind.Stay);
        FenceItemDrop.TransferTargetId(fences, SourceId, 420, 10).Should().BeNull();
    }

    [Fact]
    public void Resolve_OverlappingBodies_LastInListWins()
    {
        var fences = new[]
        {
            Expanded(OtherId, 0, 0, width: 300, height: 200),
            Expanded(ThirdId, 50, 50, width: 300, height: 200)
        };

        FenceItemDrop.Resolve(fences, SourceId, 80, 100).Should().Be(FenceItemDropKind.Transfer);
        FenceItemDrop.TransferTargetId(fences, SourceId, 80, 100).Should().Be(ThirdId);
    }

    [Fact]
    public void Resolve_EmptyList_Ejects()
    {
        FenceItemDrop.Resolve([], SourceId, 10, 10).Should().Be(FenceItemDropKind.Eject);
    }

    [Fact]
    public void Resolve_PointOnOwnTitleBar_Stays()
    {
        FenceScreenTarget source = Expanded(SourceId, 0, 0);
        FenceItemDrop.Resolve([source], SourceId, 20, 10).Should().Be(FenceItemDropKind.Stay);
    }
}
