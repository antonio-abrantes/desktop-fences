using DesktopFences.Core.Fences;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class ZOrderPlacementTests
{
    [Fact]
    public void IdleOverlap_DoesNotMove_WhenHostIsBelowAndNothingCoversFromDesktop()
    {
        ZOrderPlacement.NeedsZOrderMove(
            desktopHostIsBelow: true,
            desktopBandIsAbove: false).Should().BeFalse();
    }

    [Fact]
    public void WinD_Moves_WhenDesktopBandIsAbove()
    {
        ZOrderPlacement.NeedsZOrderMove(
            desktopHostIsBelow: true,
            desktopBandIsAbove: true).Should().BeTrue();
    }

    [Fact]
    public void BuriedUnderDesktop_Moves_WhenHostIsNotBelow()
    {
        ZOrderPlacement.NeedsZOrderMove(
            desktopHostIsBelow: false,
            desktopBandIsAbove: false).Should().BeTrue();
    }

    [Fact]
    public void BehindApps_DoesNotMove_HostBelowEvenIfAnAppIsAbove()
    {
        ZOrderPlacement.AlreadyAboveDesktop(
            desktopHostIsBelow: true,
            desktopBandIsAbove: false).Should().BeTrue();
    }
}
