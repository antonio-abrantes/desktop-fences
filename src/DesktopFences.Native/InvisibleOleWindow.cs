namespace DesktopFences.Native;

/// <summary>
/// HWND layered com alpha uniforme (LWA_ALPHA): o Explorer vê sólido no OLE
/// e o usuário quase não vê. Diferente de AllowsTransparency (per-pixel).
/// Alpha 1 era ignorado no hit-test do OLE (cursor de proibido).
/// </summary>
public static class InvisibleOleWindow
{
    public const byte DropLayerAlpha = 28;
    public const int WmNcHitTest = 0x0084;
    public const int HtClient = 1;

    public static void Apply(IntPtr hwnd, byte alpha = DropLayerAlpha)
    {
        if (hwnd == IntPtr.Zero)
            return;

        long ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        NativeMethods.SetWindowLongPtr(
            hwnd,
            NativeMethods.GWL_EXSTYLE,
            (IntPtr)(ex | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TOOLWINDOW));
        NativeMethods.SetLayeredWindowAttributes(hwnd, 0, alpha, NativeMethods.LWA_ALPHA);
    }

    public static void MakeClickThrough(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        long ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        NativeMethods.SetWindowLongPtr(
            hwnd,
            NativeMethods.GWL_EXSTYLE,
            (IntPtr)(ex
                     | NativeMethods.WS_EX_LAYERED
                     | NativeMethods.WS_EX_TOOLWINDOW
                     | NativeMethods.WS_EX_TRANSPARENT
                     | NativeMethods.WS_EX_NOACTIVATE));
    }

    public static void PlaceTopMost(IntPtr hwnd, int x, int y, int width, int height)
    {
        if (hwnd == IntPtr.Zero)
            return;

        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            x, y, width, height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    public static void RaiseTopMost(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }
}
