using DesktopFences.Core.Fences;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class CreateFenceOnColdStartTests
{
    [Fact]
    public void EmptyLayout_DoesNotAddAnother_EnsureAtLeastOneIsTheRequestedFence()
    {
        CreateFenceOnColdStart.ShouldAddAnother(0).Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    public void ExistingFences_AddsAnother(int existingCount)
    {
        CreateFenceOnColdStart.ShouldAddAnother(existingCount).Should().BeTrue();
    }
}
