using DesktopFences.Core.Fences;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class ZOrderPlacementTests
{
    [Fact]
    public void NeedsZOrderMove_IsFalse_WhenNeighborMatches()
    {
        ZOrderPlacement.NeedsZOrderMove(42, 42).Should().BeFalse();
    }

    [Fact]
    public void NeedsZOrderMove_IsTrue_WhenNeighborDiffers()
    {
        ZOrderPlacement.NeedsZOrderMove(42, 99).Should().BeTrue();
    }

    [Fact]
    public void NeedsZOrderMove_IsTrue_WhenInsertAfterIsTop()
    {
        ZOrderPlacement.NeedsZOrderMove(42, 0).Should().BeTrue();
    }
}
