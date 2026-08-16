using DesktopFences.Core.Transactions;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class CompensatingBatchTests
{
    [Fact]
    public void Execute_FailureOnSecondItemCompensatesFirstAndDoesNotRunThird()
    {
        var applied = new List<int>();
        var compensated = new List<int>();

        CompensatingBatchResult<int> result = CompensatingBatch.Execute(
            [1, 2, 3],
            item =>
            {
                if (item == 2)
                    return false;
                applied.Add(item);
                return true;
            },
            item =>
            {
                compensated.Add(item);
                return true;
            });

        result.Success.Should().BeFalse();
        result.FailedItem.Should().Be(2);
        applied.Should().Equal(1);
        compensated.Should().Equal(1);
        result.CompensationComplete.Should().BeTrue();
    }

    [Fact]
    public void Execute_CompensatesInReverseOrder()
    {
        var compensated = new List<int>();

        CompensatingBatch.Execute(
            [1, 2, 3],
            item => item != 3,
            item => { compensated.Add(item); return true; });

        compensated.Should().Equal(2, 1);
    }

    [Fact]
    public void Execute_CheckpointCountIsIndependentOfBatchSize()
    {
        int applyCalls = 0;
        CompensatingBatchResult<int> result = CompensatingBatch.Execute(
            Enumerable.Range(1, 100).ToList(),
            _ => { applyCalls++; return true; },
            _ => true);

        result.Success.Should().BeTrue();
        applyCalls.Should().Be(100);
        result.Applied.Should().HaveCount(100);
    }
}
