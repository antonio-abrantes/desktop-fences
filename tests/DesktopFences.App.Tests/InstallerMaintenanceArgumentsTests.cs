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
    [InlineData("remove", false)]
    [InlineData("prepare-exit ", false)]
    [InlineData(null, false)]
    public void LocalPipeAcceptsOnlyTheNonDestructiveExitCommand(string? command, bool expected)
    {
        MaintenanceProtocol.IsPrepareExitCommand(command).Should().Be(expected);
    }
}
