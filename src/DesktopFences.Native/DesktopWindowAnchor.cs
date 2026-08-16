using System.Runtime.InteropServices;

namespace DesktopFences.Native;

/// <summary>
/// Z-order da fence: janela top-level (pra receber drop do Explorer) mas atrás
/// dos apps. Não usamos GWL_HWNDPARENT no Progman — isso impedia o OLE drop.
/// </summary>
public static class DesktopWindowAnchor
{
    private static uint _shellHookMsg;

    public static uint ShellHookMessage
    {
        get
        {
            if (_shellHookMsg == 0)
                _shellHookMsg = NativeMethods.RegisterWindowMessage("SHELLHOOK");
            return _shellHookMsg;
        }
    }

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

    public static void ExcludeFromPeek(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        int yes = 1;
        NativeMethods.DwmSetWindowAttribute(
            hwnd, NativeMethods.DWMWA_EXCLUDED_FROM_PEEK, ref yes, sizeof(int));
        NativeMethods.DwmSetWindowAttribute(
            hwnd, NativeMethods.DWMWA_DISALLOW_PEEK, ref yes, sizeof(int));
    }

    public static void RegisterDesktopHook(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;
        _ = ShellHookMessage;
        NativeMethods.RegisterShellHookWindow(hwnd);
    }

    public static void UnregisterDesktopHook(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;
        NativeMethods.DeregisterShellHookWindow(hwnd);
    }

    public static bool IsMinimizeCommand(IntPtr wParam) =>
        ((int)(long)wParam & 0xFFF0) == NativeMethods.SC_MINIMIZE;

    public static IntPtr FilterDesktopSurvival(
        IntPtr hwnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        bool stayOnDesktop,
        out bool handled)
    {
        handled = false;
        if (!stayOnDesktop)
            return IntPtr.Zero;

        if (msg == NativeMethods.WM_SYSCOMMAND && IsMinimizeCommand(wParam))
        {
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == NativeMethods.WM_SHOWWINDOW && wParam == IntPtr.Zero)
        {
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == NativeMethods.WM_SIZE && (int)(long)wParam == NativeMethods.SIZE_MINIMIZED)
        {
            handled = true;
            KeepOnDesktop(hwnd);
            return IntPtr.Zero;
        }

        if (msg == NativeMethods.WM_WINDOWPOSCHANGING)
            KeepVisible(lParam);

        if (msg == (int)ShellHookMessage)
            KeepOnDesktop(hwnd);

        return IntPtr.Zero;
    }

    /// <summary>
    /// Win+D / Mostrar ambiente de trabalho manda SWP_HIDEWINDOW. Tirar a flag
    /// no WINDOWPOSCHANGING impede a fence de sumir. Coordenadas de minimize
    /// (-32000) também são ignoradas.
    /// </summary>
    public static void KeepVisible(IntPtr windowPosLParam)
    {
        if (windowPosLParam == IntPtr.Zero)
            return;

        var pos = Marshal.PtrToStructure<NativeMethods.WINDOWPOS>(windowPosLParam);
        bool changed = false;
        if ((pos.flags & NativeMethods.SWP_HIDEWINDOW) != 0)
        {
            pos.flags &= ~NativeMethods.SWP_HIDEWINDOW;
            pos.flags |= NativeMethods.SWP_NOZORDER;
            changed = true;
        }

        if ((pos.flags & NativeMethods.SWP_NOMOVE) == 0
            && (pos.x <= IconHideSlotPark || pos.y <= IconHideSlotPark))
        {
            pos.flags |= NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE;
            changed = true;
        }

        if (changed)
            Marshal.StructureToPtr(pos, windowPosLParam, fDeleteOld: false);
    }

    public static void KeepOnDesktop(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        bool stolen = false;
        if (NativeMethods.DwmGetWindowAttribute(
                hwnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0
            && cloaked != 0)
        {
            int off = 0;
            NativeMethods.DwmSetWindowAttribute(
                hwnd, NativeMethods.DWMWA_CLOAK, ref off, sizeof(int));
            stolen = true;
        }

        if (NativeMethods.IsIconic(hwnd) || !NativeMethods.IsWindowVisible(hwnd))
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNOACTIVATE);
            stolen = true;
        }

        if (stolen)
            SendBehindApps(hwnd);
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

    private const int IconHideSlotPark = -10_000;
}
