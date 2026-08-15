namespace DesktopFences.Native;

/// <summary>
/// Z-order da fence: janela top-level (pra receber drop do Explorer) mas atrás
/// dos apps. Não usamos GWL_HWNDPARENT no Progman — isso impedia o OLE drop.
/// </summary>
public static class DesktopWindowAnchor
{
    public static void SendBehindApps(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_BOTTOM,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    public static void PreventMinimizeMaximize(IntPtr hwnd)
    {
        IntPtr style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE);
        long updated = style.ToInt64() & ~NativeMethods.WS_MINIMIZEBOX & ~NativeMethods.WS_MAXIMIZEBOX;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE, (IntPtr)updated);
    }

    public static void HideFromTaskSwitchers(IntPtr hwnd)
    {
        IntPtr ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        long updated = (ex.ToInt64() | NativeMethods.WS_EX_TOOLWINDOW) & ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, (IntPtr)updated);
    }
}
