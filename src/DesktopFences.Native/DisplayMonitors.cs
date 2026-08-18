using System.Runtime.InteropServices;

namespace DesktopFences.Native;

/// <summary>
/// Monitores enumerados com <c>szDevice</c> estável (ex. \\.\DISPLAY2).
/// </summary>
public static class DisplayMonitors
{
    public readonly record struct Info(
        string DeviceName,
        bool IsPrimary,
        MonitorWorkArea.Pixels WorkArea);

    public static IReadOnlyList<Info> Enumerate()
    {
        var monitors = new List<Info>();
        NativeMethods.MonitorEnumProc proc = (hMonitor, _, _, _) =>
        {
            var info = new NativeMethods.MONITORINFOEX
            {
                cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>()
            };
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
                return true;

            NativeMethods.RECT work = info.rcWork;
            int width = work.right - work.left;
            int height = work.bottom - work.top;
            if (width <= 0 || height <= 0)
                return true;

            monitors.Add(new Info(
                info.szDevice,
                (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0,
                new MonitorWorkArea.Pixels(work.left, work.top, width, height)));
            return true;
        };

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, proc, IntPtr.Zero);
        GC.KeepAlive(proc);
        return monitors;
    }

    public static IReadOnlyList<string> DeviceNames() =>
        Enumerate().Select(m => m.DeviceName).ToList();

    public static string? DeviceNameForWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return null;

        IntPtr monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
            return null;

        var info = new NativeMethods.MONITORINFOEX
        {
            cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>()
        };
        return NativeMethods.GetMonitorInfo(monitor, ref info) ? info.szDevice : null;
    }

    public static Info? Primary()
    {
        IReadOnlyList<Info> monitors = Enumerate();
        Info? fallback = null;
        foreach (Info monitor in monitors)
        {
            if (monitor.IsPrimary)
                return monitor;
            fallback ??= monitor;
        }

        return fallback;
    }

    public static bool TryGetPrimaryWorkAreaPixels(out MonitorWorkArea.Pixels workArea)
    {
        Info? primary = Primary();
        if (primary is null)
        {
            workArea = default;
            return false;
        }

        workArea = primary.Value.WorkArea;
        return true;
    }
}
