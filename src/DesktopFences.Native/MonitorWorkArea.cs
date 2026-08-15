using System.Runtime.InteropServices;

namespace DesktopFences.Native;

/// <summary>
/// Área útil do monitor (rcWork: desktop menos a taskbar). Coordenadas em pixels de ecrã.
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
}
