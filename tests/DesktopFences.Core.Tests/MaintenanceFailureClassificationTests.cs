using DesktopFences.Core.Install;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class MaintenanceFailureClassificationTests
{
    [Theory]
    [InlineData(true, false, true, true, MaintenanceFailureKind.InstanceBusy)]
    [InlineData(true, false, false, false, MaintenanceFailureKind.InstanceBusy)]
    [InlineData(true, true, false, true, MaintenanceFailureKind.CustodyBlocked)]
    [InlineData(false, true, false, true, MaintenanceFailureKind.CustodyBlocked)]
    [InlineData(false, true, true, false, MaintenanceFailureKind.CustodyBlocked)]
    [InlineData(false, false, true, false, MaintenanceFailureKind.CustodyBlocked)]
    [InlineData(false, true, true, true, MaintenanceFailureKind.None)]
    [InlineData(true, true, true, true, MaintenanceFailureKind.None)]
    public void Classify_Table(
        bool mutexHeldByOther,
        bool pipeOk,
        bool recoverComplete,
        bool outboundOk,
        MaintenanceFailureKind expected)
    {
        MaintenanceFailureClassification.Classify(
            mutexHeldByOther,
            pipeOk,
            recoverComplete,
            outboundOk).Should().Be(expected);
    }

    [Fact]
    public void ExitCodes_MatchKind()
    {
        MaintenanceFailureClassification.ExitCode(MaintenanceFailureKind.None).Should().Be(0);
        MaintenanceFailureClassification.ExitCode(MaintenanceFailureKind.InstanceBusy).Should().Be(1);
        MaintenanceFailureClassification.ExitCode(MaintenanceFailureKind.InvalidRequest).Should().Be(2);
        MaintenanceFailureClassification.ExitCode(MaintenanceFailureKind.CustodyBlocked).Should().Be(3);
    }
}

public sealed class InstallerCustodyRulesTests
{
    [Theory]
    [InlineData("finalize", false)]
    [InlineData("keep", false)]
    [InlineData("upgradekeep", false)]
    [InlineData("reset", true)]
    [InlineData("uninstallkeep", true)]
    [InlineData("remove", true)]
    [InlineData("RESET", true)]
    [InlineData(null, false)]
    public void ReleasesCustody_OnlyWhenItemsMustReturnToDesktop(string? mode, bool expected)
    {
        InstallerCustodyRules.ReleasesCustody(mode).Should().Be(expected);
    }

    [Theory]
    [InlineData("upgradekeep", true)]
    [InlineData("keep", true)]
    [InlineData("reset", false)]
    [InlineData("uninstallkeep", false)]
    [InlineData("remove", false)]
    [InlineData("finalize", false)]
    public void UsesUpgradeExit_ForKeepUpgradeOnly(string mode, bool expected)
    {
        InstallerCustodyRules.UsesUpgradeExit(mode).Should().Be(expected);
    }
}

public sealed class MaintenanceResultRecordTests
{
    [Fact]
    public void ToLogText_IsSingleLineFields_AndTryReadKind()
    {
        var record = new MaintenanceResultRecord(
            DateTimeOffset.Parse("2026-08-19T16:00:00Z"),
            "upgradekeep",
            MaintenanceFailureKind.InstanceBusy,
            1,
            "line1\nline2");

        string text = record.ToLogText();
        text.Should().Contain("mode=upgradekeep");
        text.Should().Contain("kind=InstanceBusy");
        text.Should().Contain("exitCode=1");
        text.Should().NotContain("\nline2");

        MaintenanceResultRecord.TryReadKind(text, out MaintenanceFailureKind kind).Should().BeTrue();
        kind.Should().Be(MaintenanceFailureKind.InstanceBusy);
    }
}
