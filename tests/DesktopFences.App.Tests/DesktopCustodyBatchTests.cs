using System.IO;
using System.Diagnostics;
using DesktopFences.Core.Models;
using DesktopFences.Native;
using FluentAssertions;
using Microsoft.Win32;
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

    [Fact]
    public void ExecuteInbound_AlreadyAtDestination_DoesNotRequestShellNotify()
    {
        DesktopCustodyPlan plan = CreatePlan(1);
        Directory.CreateDirectory(Path.GetDirectoryName(plan.DestinationPath!)!);
        File.WriteAllText(plan.DestinationPath!, "já no store");
        plan = plan with { SourcePath = plan.DestinationPath };

        DesktopCustodyBatchResult result = new DesktopCustodyBatch().ExecuteInbound([plan]);

        result.Success.Should().BeTrue(result.Error);
        result.Notify.Should().Be(ShellNotifyRequest.None);
        File.Exists(plan.DestinationPath!).Should().BeTrue();
    }

    [Fact]
    public void ExecuteInbound_PhysicalMove_RequestsDirectoryNotifyOnly()
    {
        DesktopCustodyPlan plan = CreatePlanWithSource(1);

        DesktopCustodyBatchResult result = new DesktopCustodyBatch().ExecuteInbound([plan]);

        result.Success.Should().BeTrue(result.Error);
        result.Notify.UpdateDirectory.Should().BeTrue();
        result.Notify.AssocChanged.Should().BeFalse();
    }

    [Fact]
    public void ExecuteInbound_Namespace_RequestsAssocOnlyWhenRegistryChanges()
    {
        string canonical = "{B2C3D4E5-F617-8901-BCDE-F12345678901}";
        var plan = new DesktopCustodyPlan(
            Guid.NewGuid(), FenceItemKind.Namespace, "Synthetic2", null, null, null, null, canonical);
        var batch = new DesktopCustodyBatch();

        try
        {
            DesktopCustodyBatchResult first = batch.ExecuteInbound([plan]);
            first.Success.Should().BeTrue(first.Error);
            first.Notify.UpdateDirectory.Should().BeFalse();
            first.Notify.AssocChanged.Should().BeTrue();

            DesktopCustodyBatchResult second = batch.ExecuteInbound([plan]);
            second.Success.Should().BeTrue(second.Error);
            second.Notify.Should().Be(ShellNotifyRequest.None);

            DesktopCustodyBatchResult shown = batch.ExecuteOutbound([plan]);
            shown.Success.Should().BeTrue(shown.Error);
            shown.Notify.AssocChanged.Should().BeTrue();
            shown.Notify.UpdateDirectory.Should().BeFalse();
        }
        finally
        {
            const string newPanel =
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
            const string classic =
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\ClassicStartMenu";
            foreach (string subkey in new[] { newPanel, classic })
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subkey, writable: true);
                try { key?.DeleteValue(canonical, throwOnMissingValue: false); } catch { }
            }
        }
    }

    [Theory]
    [InlineData("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "{20D04FE0-3AEA-1069-A2D8-08002B30309D}")]
    [InlineData("{645FF040-5081-101B-9F08-00AA002F954E}", "{645FF040-5081-101B-9F08-00AA002F954E}")]
    [InlineData("shell:NetworkPlacesFolder", "{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}")]
    public void PlanInbound_Namespace_NormalizesRegistryKey(string raw, string expected)
    {
        var item = new DesktopCustodyItem(
            Guid.NewGuid(), FenceItemKind.Namespace, "Sistema", raw, null, null);

        IReadOnlyList<DesktopCustodyPlan> plans = new DesktopCustodyBatch().PlanInbound([item]);

        plans.Should().ContainSingle();
        plans[0].Kind.Should().Be(FenceItemKind.Namespace);
        plans[0].NamespaceKey.Should().Be(expected);
    }

    [Fact]
    public void PlanOutbound_Namespace_NormalizesDoubleColonPrefix()
    {
        var item = new DesktopCustodyItem(
            Guid.NewGuid(),
            FenceItemKind.Namespace,
            "Este computador",
            "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
            null,
            null);

        IReadOnlyList<DesktopCustodyPlan> plans = new DesktopCustodyBatch().PlanOutbound([item]);

        plans.Single().NamespaceKey.Should().Be("{20D04FE0-3AEA-1069-A2D8-08002B30309D}");
    }

    [Fact]
    public void PlanInbound_Namespace_RejectsUnnormalizableKey()
    {
        var item = new DesktopCustodyItem(
            Guid.NewGuid(), FenceItemKind.Namespace, "X", "not-a-clsid", null, null);

        FluentActions.Invoking(() => new DesktopCustodyBatch().PlanInbound([item]))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*namespace*");
    }

    [Fact]
    public void ExecuteInbound_Namespace_WritesCanonicalKeyAndRemovesLegacyDoubleColon()
    {
        // GUID sintético: não esconde ícones reais do Desktop do programador.
        string canonical = "{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}";
        string legacy = "::" + canonical;
        const string newPanel =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
        const string classic =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\ClassicStartMenu";
        var plan = new DesktopCustodyPlan(
            Guid.NewGuid(), FenceItemKind.Namespace, "Synthetic", null, null, null, null, legacy);

        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(newPanel)!)
                key.SetValue(legacy, 1, RegistryValueKind.DWord);
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(classic)!)
                key.SetValue(legacy, 1, RegistryValueKind.DWord);

            DesktopCustodyBatchResult hidden = new DesktopCustodyBatch().ExecuteInbound([plan]);
            hidden.Success.Should().BeTrue(hidden.Error);
            hidden.Notify.AssocChanged.Should().BeTrue();
            hidden.Notify.UpdateDirectory.Should().BeFalse();

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(newPanel)!)
            {
                key.GetValue(canonical).Should().Be(1);
                key.GetValue(legacy).Should().BeNull();
            }

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(classic)!)
            {
                key.GetValue(canonical).Should().Be(1);
                key.GetValue(legacy).Should().BeNull();
            }

            DesktopCustodyBatchResult shown = new DesktopCustodyBatch().ExecuteOutbound([plan]);
            shown.Success.Should().BeTrue(shown.Error);
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(newPanel)!)
                key.GetValue(canonical).Should().Be(0);
        }
        finally
        {
            foreach (string subkey in new[] { newPanel, classic })
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subkey, writable: true);
                try { key?.DeleteValue(canonical, throwOnMissingValue: false); } catch { }
                try { key?.DeleteValue(legacy, throwOnMissingValue: false); } catch { }
            }
        }
    }

    [Fact]
    public void ExecuteInbound_Namespace_RejectsUnnormalizableKeyWithoutWriting()
    {
        var plan = new DesktopCustodyPlan(
            Guid.NewGuid(), FenceItemKind.Namespace, "X", null, null, null, null, "::{not-valid}");

        DesktopCustodyBatchResult result = new DesktopCustodyBatch().ExecuteInbound([plan]);

        result.Success.Should().BeFalse();
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
