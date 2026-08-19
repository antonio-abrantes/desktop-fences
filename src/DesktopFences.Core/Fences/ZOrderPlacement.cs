namespace DesktopFences.Core.Fences;

/// <summary>
/// Skip de SetWindowPos em idle. HWND_TOP no Win32 é 0 — a regra antiga
/// (vizinho == insertAfter, e 0 = mover sempre) nunca saltava o caso comum
/// e ainda empurrava todas as fences para o mesmo sítio a 1 Hz.
/// </summary>
public static class ZOrderPlacement
{
    /// <summary>
    /// A fence já está na banda certa: o host do Desktop está abaixo
    /// (só fences / WorkerW / Progman pelo caminho) e nenhum host de
    /// wallpaper ficou por cima (Win+D). Irmãs fences acima ou abaixo
    /// são válidas — não se reordenam em idle.
    /// </summary>
    public static bool AlreadyAboveDesktop(bool desktopHostIsBelow, bool desktopBandIsAbove) =>
        desktopHostIsBelow && !desktopBandIsAbove;

    public static bool NeedsZOrderMove(bool desktopHostIsBelow, bool desktopBandIsAbove) =>
        !AlreadyAboveDesktop(desktopHostIsBelow, desktopBandIsAbove);
}
