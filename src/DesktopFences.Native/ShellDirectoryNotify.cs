namespace DesktopFences.Native;

/// <summary>
/// Notificações pontuais à Shell. O stub Novo usa só UPDATEDIR; o ProgID usa ASSOCCHANGED.
/// </summary>
public static class ShellDirectoryNotify
{
    public static void NotifyUpdated(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return;

        NativeMethods.SHChangeNotify(
            NativeMethods.SHCNE_UPDATEDIR,
            NativeMethods.SHCNF_PATHW | NativeMethods.SHCNF_FLUSH,
            directory,
            IntPtr.Zero);
    }

    public static void NotifyAssociationsChanged()
    {
        NativeMethods.SHChangeNotify(
            NativeMethods.SHCNE_ASSOCCHANGED,
            NativeMethods.SHCNF_IDLIST | NativeMethods.SHCNF_FLUSH,
            IntPtr.Zero,
            IntPtr.Zero);
    }
}
