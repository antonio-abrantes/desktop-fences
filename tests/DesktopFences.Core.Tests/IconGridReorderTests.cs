using DesktopFences.Core.Occupancy;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class IconGridReorderTests
{
    [Fact]
    public void InsertIndex_EmptyGrid_ReturnsZero()
    {
        IconGridReorder.InsertIndex(0, 10, 10, 88, 84, 360).Should().Be(0);
    }

    [Fact]
    public void InsertIndex_LeftHalfOfFirstCell_IsZero()
    {
        IconGridReorder.InsertIndex(4, 20, 10, 88, 84, 360).Should().Be(0);
    }

    [Fact]
    public void InsertIndex_RightHalfOfFirstCell_IsOne()
    {
        IconGridReorder.InsertIndex(4, 60, 10, 88, 84, 360).Should().Be(1);
    }

    [Fact]
    public void InsertIndex_SecondRow_UsesColumnCount()
    {
        // 360/88 → 4 colunas; y=90 → linha 1; x=20 → col 0 → índice 4, clamped a count
        IconGridReorder.InsertIndex(6, 20, 90, 88, 84, 360).Should().Be(4);
    }

    [Fact]
    public void MoveBlock_ReinsertsItemsTogetherAtTarget()
    {
        var items = new List<string> { "a", "b", "c", "d" };
        IconGridReorder.MoveBlock(items, ["b", "c"], 0);
        items.Should().Equal("b", "c", "a", "d");
    }

    [Fact]
    public void MoveBlock_MovingForwardAdjustsIndex()
    {
        var items = new List<string> { "a", "b", "c", "d" };
        IconGridReorder.MoveBlock(items, ["a"], 3);
        items.Should().Equal("b", "c", "a", "d");
    }
}
