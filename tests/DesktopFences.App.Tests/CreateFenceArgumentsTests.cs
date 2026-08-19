using DesktopFences.App;
using DesktopFences.App.Services;
using FluentAssertions;
using Xunit;

namespace DesktopFences.App.Tests;

public sealed class CreateFenceArgumentsTests
{
    [Fact]
    public void FlagAlone_IsCreateFenceWithoutPath()
    {
        bool parsed = CreateFenceArguments.TryParse(["--create-fence"], out var result);

        parsed.Should().BeTrue();
        result.Should().NotBeNull();
        result!.StubPath.Should().BeNull();
    }

    [Fact]
    public void EqualsForm_CapturesPath()
    {
        bool parsed = CreateFenceArguments.TryParse(
            [@"--create-fence=C:\Users\me\Desktop\Fence.desktopfence"],
            out var result);

        parsed.Should().BeTrue();
        result!.StubPath.Should().Be(@"C:\Users\me\Desktop\Fence.desktopfence");
    }

    [Fact]
    public void SpaceForm_CapturesPath()
    {
        bool parsed = CreateFenceArguments.TryParse(
            ["--create-fence", @"C:\Users\me\Desktop\Nova fence.desktopfence"],
            out var result);

        parsed.Should().BeTrue();
        result!.StubPath.Should().Be(@"C:\Users\me\Desktop\Nova fence.desktopfence");
    }

    [Fact]
    public void QuotedPath_IsUnquoted()
    {
        bool parsed = CreateFenceArguments.TryParse(
            ["--create-fence", @"""C:\Users\me\Desktop\Fence.desktopfence"""],
            out var result);

        parsed.Should().BeTrue();
        result!.StubPath.Should().Be(@"C:\Users\me\Desktop\Fence.desktopfence");
    }

    [Fact]
    public void NextArgumentStartingWithDashDash_IsNotTreatedAsPath()
    {
        bool parsed = CreateFenceArguments.TryParse(
            ["--create-fence", "--language=pt"],
            out var result);

        parsed.Should().BeTrue();
        result!.StubPath.Should().BeNull();
    }

    [Fact]
    public void AbsentFlag_IsNotCreateFence()
    {
        CreateFenceArguments.TryParse([], out var result).Should().BeFalse();
        result.Should().BeNull();
        CreateFenceArguments.TryParse(["--other"], out _).Should().BeFalse();
    }

    [Fact]
    public void MaintenancePresent_StillParsesCreateFence_CallerMustPreferMaintenance()
    {
        string[] args = ["--maintenance=keep", "--create-fence", @"C:\Users\me\Desktop\Fence.desktopfence"];

        InstallerMaintenanceArguments.TryParse(args, out var maintenance, out var error).Should().BeTrue();
        maintenance.Should().NotBeNull();
        error.Should().BeNull();

        CreateFenceArguments.TryParse(args, out var create).Should().BeTrue();
        create.Should().NotBeNull();
    }
}
