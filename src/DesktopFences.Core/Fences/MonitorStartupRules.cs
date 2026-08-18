namespace DesktopFences.Core.Fences;

public static class MonitorStartupRules
{
    public static bool ShouldWaitForMonitor(string? savedDeviceName, IReadOnlyCollection<string> availableDeviceNames)
    {
        if (string.IsNullOrWhiteSpace(savedDeviceName))
            return false;

        foreach (string device in availableDeviceNames)
        {
            if (string.Equals(device, savedDeviceName, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
