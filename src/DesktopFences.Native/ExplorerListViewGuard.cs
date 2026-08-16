namespace DesktopFences.Native;

/// <summary>
/// Detecta SysListView32 nova depois do explorer.exe morrer. IntPtr fica aqui.
/// </summary>
public sealed class ExplorerListViewGuard
{
    private IntPtr _handle;

    public void Arm() => _handle = DesktopIconService.FindDesktopListView();

    public bool TryConsumeReconnect()
    {
        if (_handle == IntPtr.Zero)
            return false;
        if (NativeMethods.IsWindow(_handle))
            return false;

        IntPtr now = DesktopIconService.FindDesktopListView();
        if (now == IntPtr.Zero)
            return false;

        _handle = now;
        return true;
    }
}
