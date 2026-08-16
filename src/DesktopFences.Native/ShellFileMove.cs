using System.Runtime.InteropServices;

namespace DesktopFences.Native;

/// <summary>
/// Move via IFileOperation (Shell); se o COM falhar, File/Directory.Move.
/// </summary>
internal static class ShellFileMove
{
    private const uint FofSilent = 0x0004;
    private const uint FofNoConfirmation = 0x0010;
    private const uint FofNoConfirmMkdir = 0x0200;
    private const uint FofNoErrorUi = 0x0400;
    private const uint OperationFlags = FofSilent | FofNoConfirmation | FofNoConfirmMkdir | FofNoErrorUi;

    private static readonly Guid ClsidFileOperation = new("3ad05575-8857-4850-9277-11b85bdb8e09");
    private static readonly Guid IidIShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");

    public static bool Move(string source, string destFolder, string destName)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destFolder)
            || string.IsNullOrWhiteSpace(destName))
            return false;

        try
        {
            Directory.CreateDirectory(destFolder);
        }
        catch
        {
            return false;
        }

        string dest = Path.Combine(destFolder, destName);
        if (SamePath(source, dest))
            return true;

        if (TryIoMove(source, dest) || TryComMove(source, destFolder, destName))
        {
            StripHidden(dest);
            return true;
        }

        return false;
    }

    private static bool TryComMove(string source, string destFolder, string destName)
    {
        object? opsObj = null;
        object? sourceItem = null;
        object? destItem = null;
        try
        {
            Type? type = Type.GetTypeFromCLSID(ClsidFileOperation, throwOnError: false);
            if (type is null)
                return false;

            opsObj = Activator.CreateInstance(type);
            if (opsObj is not IFileOperation ops)
                return false;

            Guid iid = IidIShellItem;
            if (SHCreateItemFromParsingName(source, IntPtr.Zero, ref iid, out sourceItem) != 0
                || sourceItem is null)
                return false;
            if (SHCreateItemFromParsingName(destFolder, IntPtr.Zero, ref iid, out destItem) != 0
                || destItem is null)
                return false;

            ops.SetOperationFlags(OperationFlags);
            ops.MoveItem((IShellItem)sourceItem, (IShellItem)destItem, destName, IntPtr.Zero);
            ops.PerformOperations();
            return File.Exists(Path.Combine(destFolder, destName))
                   || Directory.Exists(Path.Combine(destFolder, destName));
        }
        catch
        {
            return false;
        }
        finally
        {
            Release(sourceItem);
            Release(destItem);
            Release(opsObj);
        }
    }

    private static bool TryIoMove(string source, string dest)
    {
        try
        {
            if (File.Exists(dest) || Directory.Exists(dest))
                return false;
            if (File.Exists(source))
            {
                File.Move(source, dest);
                return true;
            }

            if (Directory.Exists(source))
            {
                Directory.Move(source, dest);
                return true;
            }
        }
        catch
        {
            /* sem permissão / ficheiro em uso */
        }

        return false;
    }

    private static void StripHidden(string path)
    {
        try
        {
            FileAttributes attrs = File.GetAttributes(path);
            FileAttributes visible = DesktopFences.Core.Occupancy.DesktopHide.WithoutHidden(attrs);
            if (visible != attrs)
                File.SetAttributes(path, visible);
        }
        catch
        {
            /* atributos opcionais */
        }
    }

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void Release(object? com)
    {
        if (com is not null && Marshal.IsComObject(com))
            Marshal.ReleaseComObject(com);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        string pszPath,
        IntPtr pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppv);

    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport]
    [Guid("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        uint Advise(IntPtr pfops, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOperationFlags(uint dwOperationFlags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string pszMessage);
        void SetProgressDialog(IntPtr popd);
        void SetProperties(IntPtr pproparray);
        void SetOwnerWindow(IntPtr hwndParent);
        void ApplyPropertiesToItem(IShellItem psiItem);
        void ApplyPropertiesToItems(IntPtr punkItems);
        void RenameItem(IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IntPtr pfopsItem);
        void RenameItems(IntPtr pUnkItems, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
        void MoveItem(
            IShellItem psiItem,
            IShellItem psiDestinationFolder,
            [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName,
            IntPtr pfopsItem);
        void MoveItems(IntPtr punkItems, IShellItem psiDestinationFolder);
        void CopyItem(
            IShellItem psiItem,
            IShellItem psiDestinationFolder,
            [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName,
            IntPtr pfopsItem);
        void CopyItems(IntPtr punkItems, IShellItem psiDestinationFolder);
        void DeleteItem(IShellItem psiItem, IntPtr pfopsItem);
        void DeleteItems(IntPtr punkItems);
        void NewItem(
            IShellItem psiDestinationFolder,
            uint dwFileAttributes,
            [MarshalAs(UnmanagedType.LPWStr)] string pszName,
            [MarshalAs(UnmanagedType.LPWStr)] string pszTemplateName,
            IntPtr pfopsItem);
        void PerformOperations();
        void GetAnyOperationsAborted(out bool pfAnyOperationsAborted);
    }
}
