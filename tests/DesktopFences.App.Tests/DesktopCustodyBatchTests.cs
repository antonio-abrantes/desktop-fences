using System.IO;
using System.Diagnostics;
using DesktopFences.Core.Models;
using DesktopFences.Native;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DesktopFences.App.Tests;

public sealed class DesktopCustodyBatchTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "df-native-batch-" + Guid.NewGuid().ToString("N"));

    private readonly ITestOutputHelper _output;

    public DesktopCustodyBatchTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void ExecuteInbound_MovesWholeBatchAndCompensateRestoresEverySource()
    {
        IReadOnlyList<DesktopCustodyPlan> plans = Enumerable.Range(1, 3)
            .Select(CreatePlanWithSource)
            .ToList();
        var batch = new DesktopCustodyBatch();

        DesktopCustodyBatchResult moved = batch.ExecuteInbound(plans);

        moved.Success.Should().BeTrue();
        plans.Should().OnlyContain(plan =>
            !File.Exists(plan.SourcePath!) && File.Exists(plan.DestinationPath!));

        batch.Compensate(plans, wasInbound: true).Should().BeTrue();
        plans.Should().OnlyContain(plan =>
            File.Exists(plan.SourcePath!) && !File.Exists(plan.DestinationPath!));
    }

    [Fact]
    public void ExecuteInbound_FailureOnSecondItemCompensatesFirstOnDisk()
    {
        DesktopCustodyPlan first = CreatePlanWithSource(1);
        DesktopCustodyPlan missing = CreatePlan(2);
        var batch = new DesktopCustodyBatch();

        DesktopCustodyBatchResult result = batch.ExecuteInbound([first, missing]);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("compensado");
        File.Exists(first.SourcePath!).Should().BeTrue();
        File.Exists(first.DestinationPath!).Should().BeFalse();
        File.Exists(missing.SourcePath!).Should().BeFalse();
        File.Exists(missing.DestinationPath!).Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public void ExecuteInbound_SameVolumeMatrixMovesEachPayloadExactlyOnce(int count)
    {
        IReadOnlyList<DesktopCustodyPlan> plans = Enumerable.Range(1, count)
            .Select(CreatePlanWithSource)
            .ToList();
        var batch = new DesktopCustodyBatch();
        var stopwatch = Stopwatch.StartNew();

        DesktopCustodyBatchResult result = batch.ExecuteInbound(plans);
        stopwatch.Stop();

        result.Success.Should().BeTrue();
        result.Applied.Should().HaveCount(count);
        plans.Count(plan => File.Exists(plan.DestinationPath!)).Should().Be(count);
        plans.Count(plan => File.Exists(plan.SourcePath!)).Should().Be(0);
        _output.WriteLine($"lote={count}; elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3}");
    }

    private DesktopCustodyPlan CreatePlanWithSource(int number)
    {
        DesktopCustodyPlan plan = CreatePlan(number);
        Directory.CreateDirectory(Path.GetDirectoryName(plan.SourcePath!)!);
        File.WriteAllText(plan.SourcePath!, $"payload-{number}");
        return plan;
    }

    private DesktopCustodyPlan CreatePlan(int number)
    {
        Guid itemId = Guid.NewGuid();
        string name = $"item-{number}.txt";
        return new DesktopCustodyPlan(
            itemId,
            FenceItemKind.Stored,
            name,
            Path.Combine(_root, "Desktop", name),
            Path.Combine(_root, "Items", itemId.ToString("D"), name),
            Path.Combine(_root, "Desktop", name),
            name,
            null);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch { }
    }
}
