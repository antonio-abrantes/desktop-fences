using System.Runtime.InteropServices;
using DesktopFences.Core.Models;
using DesktopFences.Core.Occupancy;

namespace DesktopFences.Native;

/// <summary>
/// Área útil do monitor (rcWork: desktop menos a taskbar). Coordenadas em pixels de ecrã
/// ou, via <see cref="DisplaysInListViewClient"/>, no espaço do SysListView32.
/// </summary>
public static class MonitorWorkArea
{
    public readonly record struct Pixels(int X, int Y, int Width, int Height);

    public static Pixels ForWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return default;

        IntPtr monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
            return default;

        var info = new NativeMethods.MONITORINFO
        {
            cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
        };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            return default;

        NativeMethods.RECT work = info.rcWork;
        int width = work.right - work.left;
        int height = work.bottom - work.top;
        if (width <= 0 || height <= 0)
            return default;

        return new Pixels(work.left, work.top, width, height);
    }

    public static IReadOnlyList<DisplaySurface> DisplaysInListViewClient(IntPtr listView)
    {
        if (listView == IntPtr.Zero)
            return [];

        var origin = new NativeMethods.POINT { X = 0, Y = 0 };
        if (!NativeMethods.ClientToScreen(listView, ref origin))
            return [];

        var displays = new List<DisplaySurface>();
        NativeMethods.MonitorEnumProc proc = (hMonitor, _, _, _) =>
        {
            var info = new NativeMethods.MONITORINFO
            {
                cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
            };
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
                return true;

            displays.Add(new DisplaySurface(
                ToClient(info.rcMonitor, origin),
                ToClient(info.rcWork, origin)));
            return true;
        };

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, proc, IntPtr.Zero);
        GC.KeepAlive(proc);
        return displays;
    }

    public static IReadOnlyList<PixelRect> WorkAreasInListViewClient(IntPtr listView)
    {
        IReadOnlyList<DisplaySurface> displays = DisplaysInListViewClient(listView);
        if (displays.Count == 0)
            return [];

        var work = new PixelRect[displays.Count];
        for (int i = 0; i < displays.Count; i++)
            work[i] = displays[i].Work;
        return work;
    }

    private static PixelRect ToClient(NativeMethods.RECT screen, NativeMethods.POINT origin)
    {
        int x = screen.left - origin.X;
        int y = screen.top - origin.Y;
        return new PixelRect(x, y, screen.right - screen.left, screen.bottom - screen.top);
    }
}
