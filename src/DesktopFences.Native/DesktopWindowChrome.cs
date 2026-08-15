using System.Runtime.InteropServices;

namespace DesktopFences.Native;

/// <summary>
/// Acrylic/blur + cantos DWM. Exige AllowsTransparency=False (ver ADR-0003).
/// </summary>
public static class DesktopWindowChrome
{
    /// <param name="tintArgb">ARGB do tint (alpha controla opacidade do acrylic).</param>
    public static void Apply(IntPtr hwnd, uint tintArgb = 0xCC1A1A1A)
    {
        if (hwnd == IntPtr.Zero)
            return;

        EnableRoundedCorners(hwnd);
        EnableAcrylic(hwnd, tintArgb);
    }

    public static void EnableRoundedCorners(IntPtr hwnd)
    {
        int preference = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
            ref preference,
            sizeof(int));
    }

    public static void EnableAcrylic(IntPtr hwnd, uint tintArgb)
    {
        // GradientColor da AccentPolicy é ABGR.
        uint abgr = ToAbgr(tintArgb);

        if (!TrySetAccent(hwnd, NativeMethods.AccentState.EnableAcrylicBlurBehind, abgr))
            TrySetAccent(hwnd, NativeMethods.AccentState.EnableBlurBehind, abgr);
    }

    private static bool TrySetAccent(IntPtr hwnd, NativeMethods.AccentState state, uint gradientAbgr)
    {
        var accent = new NativeMethods.AccentPolicy
        {
            AccentState = state,
            AccentFlags = 2,
            GradientColor = gradientAbgr
        };

        int size = Marshal.SizeOf(accent);
        IntPtr accentPtr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);
            var data = new NativeMethods.WindowCompositionAttributeData
            {
                Attribute = NativeMethods.WCA_ACCENT_POLICY,
                SizeOfData = size,
                Data = accentPtr
            };
            return NativeMethods.SetWindowCompositionAttribute(hwnd, ref data) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }

    private static uint ToAbgr(uint argb)
    {
        byte a = (byte)((argb >> 24) & 0xFF);
        byte r = (byte)((argb >> 16) & 0xFF);
        byte g = (byte)((argb >> 8) & 0xFF);
        byte b = (byte)(argb & 0xFF);
        return (uint)((a << 24) | (b << 16) | (g << 8) | r);
    }
}
