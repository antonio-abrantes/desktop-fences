using System.Runtime.InteropServices;

namespace DesktopFences.Native;

/// <summary>
/// O Explorer (outro processo) chama GiveFeedback e força IDC_NO enquanto o
/// hotspot está sobre uma janela layered/sem IDropTarget. SetCursor nosso
/// perde na hora; trocar OCR_NO no sistema + reaplicar a seta no pump
/// deixa o ponteiro igual ao arraste interno (só a seta, sem “proibido”).
/// SPI_SETCURSORS devolve os cursores do tema no fim do arraste.
/// </summary>
public static class DragCursorOverride
{
    private const int IdcArrow = 32512;
    private const uint OcrNo = 32648;
    private const uint SpiSetCursors = 0x0057;

    private static IntPtr _arrow;
    private static bool _replaced;

    public static void Begin()
    {
        _arrow = LoadCursor(IntPtr.Zero, IdcArrow);
        if (_arrow != IntPtr.Zero)
            SetCursor(_arrow);

        if (_replaced)
            return;

        IntPtr copy = CopyIcon(_arrow);
        if (copy == IntPtr.Zero)
            return;

        if (SetSystemCursor(copy, OcrNo))
            _replaced = true;
    }

    public static void Pulse()
    {
        if (_arrow != IntPtr.Zero)
            SetCursor(_arrow);
    }

    public static void End()
    {
        if (_replaced)
        {
            SystemParametersInfo(SpiSetCursors, 0, IntPtr.Zero, 0);
            _replaced = false;
        }

        _arrow = IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll")]
    private static extern IntPtr CopyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern bool SetSystemCursor(IntPtr hcur, uint id);

    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr hCursor);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
}
