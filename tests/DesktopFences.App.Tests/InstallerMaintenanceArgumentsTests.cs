using DesktopFences.App;
using DesktopFences.App.Services;
using FluentAssertions;
using Xunit;

namespace DesktopFences.App.Tests;

public sealed class InstallerMaintenanceArgumentsTests
{
    [Fact]
    public void NoMaintenanceArgumentLeavesNormalStartupUntouched()
    {
        bool parsed = InstallerMaintenanceArguments.TryParse([], out var result, out var error);

        parsed.Should().BeFalse();
        result.Should().BeNull();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("keep", "Keep", "pt")]
    [InlineData("upgradekeep", "UpgradeKeep", "en")]
    [InlineData("reset", "Reset", "en")]
    [InlineData("uninstallkeep", "UninstallKeep", null)]
    [InlineData("remove", "Remove", null)]
    [InlineData("finalize", "Finalize", "pt")]
    public void KnownModesAndLanguagesAreAccepted(
        string mode,
        string expected,
        string? language)
    {
        var args = new List<string> { $"--maintenance={mode}" };
        if (language is not null)
            args.Add($"--language={language}");

        bool parsed = InstallerMaintenanceArguments.TryParse(args, out var result, out var error);

        parsed.Should().BeTrue();
        error.Should().BeNull();
        result.Should().NotBeNull();
        result!.Mode.ToString().Should().Be(expected);
        result.Language.Should().Be(language);
    }

    [Theory]
    [InlineData("unknown", "pt")]
    [InlineData("keep", "fr")]
    public void UnknownValuesAreRejected(string mode, string language)
    {
        bool parsed = InstallerMaintenanceArguments.TryParse(
            [$"--maintenance={mode}", $"--language={language}"],
            out var result,
            out var error);

        parsed.Should().BeTrue();
        result.Should().BeNull();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("prepare-exit", true)]
    [InlineData("prepare-exit-upgrade", false)]
    [InlineData("create-fence", false)]
    [InlineData("remove", false)]
    [InlineData("prepare-exit ", false)]
    [InlineData(null, false)]
    public void LocalPipeTreatsOnlyPrepareExitAsDestructiveShutdown(string? command, bool expected)
    {
        MaintenanceProtocol.IsDestructiveShutdownCommand(command).Should().Be(expected);
    }

    [Fact]
    public void WillReleaseCustody_KeepsUpgradeWithoutOutbound()
    {
        InstallerMaintenance.WillReleaseCustody(InstallerMaintenanceMode.UpgradeKeep).Should().BeFalse();
        InstallerMaintenance.WillReleaseCustody(InstallerMaintenanceMode.Keep).Should().BeFalse();
        InstallerMaintenance.WillReleaseCustody(InstallerMaintenanceMode.Finalize).Should().BeFalse();
        InstallerMaintenance.WillReleaseCustody(InstallerMaintenanceMode.Reset).Should().BeTrue();
        InstallerMaintenance.WillReleaseCustody(InstallerMaintenanceMode.Remove).Should().BeTrue();
        InstallerMaintenance.WillReleaseCustody(InstallerMaintenanceMode.UninstallKeep).Should().BeTrue();
    }

    [Fact]
    public void CreateFenceCommand_SucceedsWithoutShutdown()
    {
        bool added = false;
        MaintenanceDispatch result = Dispatch(
            "create-fence",
            prepareExit: () => true,
            prepareUpgradeExit: () => throw new InvalidOperationException("upgrade não deve correr"),
            createFence: () =>
            {
                added = true;
                return true;
            });

        added.Should().BeTrue();
        result.Success.Should().BeTrue();
        result.Shutdown.Should().BeFalse();
        result.LeaveCustody.Should().BeFalse();
    }

    [Fact]
    public void CreateFenceCommand_FailedCallback_DoesNotShutdown()
    {
        MaintenanceDispatch result = Dispatch(
            "create-fence",
            prepareExit: () => true,
            prepareUpgradeExit: () => true,
            createFence: () => false);

        result.Success.Should().BeFalse();
        result.Shutdown.Should().BeFalse();
    }

    [Fact]
    public void PrepareExitCommand_Failed_DoesNotShutdown()
    {
        MaintenanceDispatch result = Dispatch(
            "prepare-exit",
            prepareExit: () => false,
            prepareUpgradeExit: () => true,
            createFence: () => true);

        result.Success.Should().BeFalse();
        result.Shutdown.Should().BeFalse();
        result.LeaveCustody.Should().BeFalse();
    }

    [Fact]
    public void PrepareExitCommand_StillShutsDownOnSuccess()
    {
        bool prepared = false;
        MaintenanceDispatch result = Dispatch(
            "prepare-exit",
            prepareExit: () =>
            {
                prepared = true;
                return true;
            },
            prepareUpgradeExit: () => throw new InvalidOperationException("upgrade não deve correr"),
            createFence: () => throw new InvalidOperationException("create-fence não deve correr"));

        prepared.Should().BeTrue();
        result.Success.Should().BeTrue();
        result.Shutdown.Should().BeTrue();
        result.LeaveCustody.Should().BeFalse();
    }

    [Fact]
    public void PrepareExitUpgrade_ShutsDownAndLeavesCustody()
    {
        bool prepared = false;
        MaintenanceDispatch result = Dispatch(
            "prepare-exit-upgrade",
            prepareExit: () => throw new InvalidOperationException("prepare-exit não deve correr"),
            prepareUpgradeExit: () =>
            {
                prepared = true;
                return true;
            },
            createFence: () => throw new InvalidOperationException("create-fence não deve correr"));

        prepared.Should().BeTrue();
        result.Success.Should().BeTrue();
        result.Shutdown.Should().BeTrue();
        result.LeaveCustody.Should().BeTrue();
    }

    [Fact]
    public void PrepareExitUpgrade_Failed_DoesNotShutdown()
    {
        MaintenanceDispatch result = Dispatch(
            "prepare-exit-upgrade",
            prepareExit: () => true,
            prepareUpgradeExit: () => false,
            createFence: () => true);

        result.Success.Should().BeFalse();
        result.Shutdown.Should().BeFalse();
        result.LeaveCustody.Should().BeFalse();
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("rm -rf")]
    [InlineData("create-fence extra")]
    [InlineData("CREATE-FENCE")]
    public void UnknownCommands_FailWithoutShutdown(string command)
    {
        MaintenanceDispatch result = Dispatch(
            command,
            prepareExit: () => true,
            prepareUpgradeExit: () => true,
            createFence: () => true);

        result.Success.Should().BeFalse();
        result.Shutdown.Should().BeFalse();
        result.LeaveCustody.Should().BeFalse();
    }

    private static MaintenanceDispatch Dispatch(
        string? command,
        Func<bool> prepareExit,
        Func<bool> prepareUpgradeExit,
        Func<bool> createFence) =>
        MaintenanceProtocol.Dispatch(command, prepareExit, prepareUpgradeExit, createFence);
}
