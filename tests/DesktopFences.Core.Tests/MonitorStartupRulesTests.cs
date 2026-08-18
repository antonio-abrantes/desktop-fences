using DesktopFences.Core.Fences;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class MonitorStartupRulesTests
{
    [Fact]
    public void ShouldWaitForMonitor_IsFalse_WhenSavedDeviceMissing()
    {
        MonitorStartupRules.ShouldWaitForMonitor(null, ["\\\\.\\DISPLAY1"]).Should().BeFalse();
        MonitorStartupRules.ShouldWaitForMonitor("", ["\\\\.\\DISPLAY1"]).Should().BeFalse();
    }

    [Fact]
    public void ShouldWaitForMonitor_IsFalse_WhenDevicePresent()
    {
        MonitorStartupRules.ShouldWaitForMonitor(
                "\\\\.\\DISPLAY2",
                ["\\\\.\\DISPLAY1", "\\\\.\\DISPLAY2"])
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldWaitForMonitor_IsTrue_WhenDeviceMissing()
    {
        MonitorStartupRules.ShouldWaitForMonitor("\\\\.\\DISPLAY3", ["\\\\.\\DISPLAY1"]).Should().BeTrue();
    }

    [Fact]
    public void ShouldWaitForMonitor_IgnoresDeviceNameCase()
    {
        MonitorStartupRules.ShouldWaitForMonitor("\\\\.\\display2", ["\\\\.\\DISPLAY2"]).Should().BeFalse();
    }
}
