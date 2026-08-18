using DesktopFences.Core.Fences;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class DesktopPlacementRetryRulesTests
{
    [Theory]
    [InlineData(1, 0, 3, false)]
    [InlineData(2, 3, 3, true)]
    [InlineData(8, 0, 3, true)]
    [InlineData(3, 2, 3, false)]
    public void IsComplete_MatchesPlacementRetryPolicy(int attempts, int positioned, int expected, bool complete)
    {
        DesktopPlacementRetryRules.IsComplete(attempts, positioned, expected).Should().Be(complete);
    }
}
