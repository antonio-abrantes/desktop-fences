using System.Runtime.InteropServices;

namespace DesktopFences.Native;

/// <summary>
/// Z-order da fence: janela top-level (pra receber drop do Explorer), acima da
/// banda Progman/WorkerW e atrás dos apps. Não usamos GWL_HWNDPARENT no Progman
/// — isso impedia o OLE drop e faria a fence depender da vida do Explorer.
/// </summary>
public static class DesktopWindowAnchor
{
    private static uint _shellHookMsg;
    private static IntPtr _desktopHost;
    private static readonly object FenceWindowLock = new();
    private static readonly HashSet<IntPtr> FenceWindows = [];

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

        if (TryPlaceAboveDesktop(hwnd))
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

        lock (FenceWindowLock)
            FenceWindows.Add(hwnd);

        _ = ShellHookMessage;
        NativeMethods.RegisterShellHookWindow(hwnd);
    }

    public static void UnregisterDesktopHook(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        lock (FenceWindowLock)
            FenceWindows.Remove(hwnd);

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

        if (NativeMethods.DwmGetWindowAttribute(
                hwnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0
            && cloaked != 0)
        {
            int off = 0;
            NativeMethods.DwmSetWindowAttribute(
                hwnd, NativeMethods.DWMWA_CLOAK, ref off, sizeof(int));
        }

        if (NativeMethods.IsIconic(hwnd) || !NativeMethods.IsWindowVisible(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNOACTIVATE);

        // Win+D pode manter IsWindowVisible=true e apenas pôr Progman/WorkerW
        // acima da fence. Reancorar sempre cobre também esse estado.
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

    private static bool TryPlaceAboveDesktop(IntPtr hwnd)
    {
        IntPtr desktopHost = FindDesktopHost();
        if (desktopHost == IntPtr.Zero)
            return false;

        // Caminhamos de baixo para cima desde a view do Desktop. Ignoramos os
        // outros hosts WorkerW/Progman e as demais fences; a primeira janela
        // restante é o app sob o qual todo o grupo de fences deve ficar.
        IntPtr insertAfter = NativeMethods.GetWindow(desktopHost, NativeMethods.GW_HWNDPREV);
        while (insertAfter != IntPtr.Zero
               && (insertAfter == hwnd
                   || IsDesktopBandWindow(insertAfter)
                   || IsFenceWindow(insertAfter)))
        {
            insertAfter = NativeMethods.GetWindow(insertAfter, NativeMethods.GW_HWNDPREV);
        }

        if (insertAfter == IntPtr.Zero || IsTopMost(insertAfter))
            insertAfter = NativeMethods.HWND_TOP;

        return NativeMethods.SetWindowPos(
            hwnd,
            insertAfter,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE
            | NativeMethods.SWP_NOSIZE
            | NativeMethods.SWP_NOACTIVATE
            | NativeMethods.SWP_NOOWNERZORDER);
    }

    private static IntPtr FindDesktopHost()
    {
        IntPtr cached = _desktopHost;
        if (ContainsDesktopView(cached))
            return cached;

        IntPtr progman = NativeMethods.FindWindow("Progman", null);
        if (ContainsDesktopView(progman))
        {
            _desktopHost = progman;
            return progman;
        }

        IntPtr found = IntPtr.Zero;
        NativeMethods.EnumWindows((candidate, _) =>
        {
            if (!ContainsDesktopView(candidate))
                return true;

            found = candidate;
            return false;
        }, IntPtr.Zero);

        _desktopHost = found;
        return found;
    }

    private static bool ContainsDesktopView(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            return false;

        IntPtr defView = NativeMethods.FindWindowEx(
            hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (defView == IntPtr.Zero)
            return false;

        return NativeMethods.FindWindowEx(
            defView, IntPtr.Zero, "SysListView32", "FolderView") != IntPtr.Zero;
    }

    private static bool IsDesktopBandWindow(IntPtr hwnd)
    {
        var className = new System.Text.StringBuilder(64);
        if (NativeMethods.GetClassName(hwnd, className, className.Capacity) == 0)
            return false;

        return className.ToString() is "Progman" or "WorkerW";
    }

    private static bool IsFenceWindow(IntPtr hwnd)
    {
        lock (FenceWindowLock)
            return FenceWindows.Contains(hwnd);
    }

    private static bool IsTopMost(IntPtr hwnd) =>
        (NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64()
         & NativeMethods.WS_EX_TOPMOST) != 0;

    private const int IconHideSlotPark = -10_000;
}
