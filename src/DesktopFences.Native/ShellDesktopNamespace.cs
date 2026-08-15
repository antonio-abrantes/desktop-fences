using System.Runtime.InteropServices;
using System.Text;

namespace DesktopFences.Native;

/// <summary>
/// Lixeira, Este computador, Rede, etc. não são ficheiros no Desktop —
/// são filhos virtuais do IShellFolder da área de trabalho (CLSID).
/// O SysListView32 só nos dá o nome visível; daí enumerar o namespace.
/// </summary>
internal static class ShellDesktopNamespace
{
    private const uint ShcontfFolders = 0x0020;
    private const uint ShcontfNonFolders = 0x0040;
    private const uint ShcontfIncludeHidden = 0x0080;
    private const uint ShgdnNormal = 0x0000;
    private const uint ShgdnInFolder = 0x0001;
    private const uint ShgdnForParsing = 0x8000;
    private const int SwShownormal = 1;

    public static byte[]? ExtractPng(string nameOrPath)
    {
        IntPtr pidl = GetAbsolutePidl(nameOrPath);
        if (pidl == IntPtr.Zero)
            return null;

        try
        {
            return ShellFileIcon.PngFromPidl(pidl);
        }
        finally
        {
            ILFree(pidl);
        }
    }

    public static bool TryExecute(string nameOrPath)
    {
        string? parsing = GetParsingName(nameOrPath);
        // Só CLSID / shell: — nunca um .lnk que calhe ter o mesmo nome visível.
        if (string.IsNullOrWhiteSpace(parsing) || !LooksLikeParsingName(parsing))
            return false;

        IntPtr rc = ShellExecute(IntPtr.Zero, "open", parsing, null, null, SwShownormal);
        return rc.ToInt64() > 32;
    }

    public static string? GetParsingName(string nameOrPath)
    {
        if (string.IsNullOrWhiteSpace(nameOrPath))
            return null;

        if (LooksLikeParsingName(nameOrPath))
            return nameOrPath.Trim();

        return FindDesktopChild(nameOrPath.Trim())?.ParsingName;
    }

    public static IntPtr GetAbsolutePidl(string nameOrPath)
    {
        if (string.IsNullOrWhiteSpace(nameOrPath))
            return IntPtr.Zero;

        string trimmed = nameOrPath.Trim().Trim('"');
        if (LooksLikeParsingName(trimmed) || Path.IsPathRooted(trimmed))
            return TryParsePidl(trimmed);

        DesktopChild? child = FindDesktopChild(trimmed);
        if (child is null || string.IsNullOrWhiteSpace(child.ParsingName))
            return IntPtr.Zero;

        return TryParsePidl(child.ParsingName);
    }

    private static IntPtr TryParsePidl(string name)
    {
        if (SHParseDisplayName(name, IntPtr.Zero, out IntPtr pidl, 0, out _) == 0
            && pidl != IntPtr.Zero)
            return pidl;

        return IntPtr.Zero;
    }

    private static DesktopChild? FindDesktopChild(string displayName)
    {
        if (SHGetDesktopFolder(out IShellFolder? desktop) != 0 || desktop is null)
            return null;

        IEnumIDList? enumerator = null;
        try
        {
            int hr = desktop.EnumObjects(
                IntPtr.Zero,
                ShcontfFolders | ShcontfNonFolders | ShcontfIncludeHidden,
                out enumerator);
            if (hr != 0 || enumerator is null)
                return null;

            while (enumerator.Next(1, out IntPtr relative, out uint fetched) == 0 && fetched == 1)
            {
                try
                {
                    string infolder = GetDisplayName(desktop, relative, ShgdnInFolder);
                    string normal = GetDisplayName(desktop, relative, ShgdnNormal);
                    if (!NamesMatch(displayName, infolder) && !NamesMatch(displayName, normal))
                        continue;

                    string parsing = GetDisplayName(desktop, relative, ShgdnForParsing);
                    return new DesktopChild(infolder, parsing);
                }
                finally
                {
                    ILFree(relative);
                }
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            if (enumerator is not null)
                Marshal.ReleaseComObject(enumerator);
            Marshal.ReleaseComObject(desktop);
        }

        return null;
    }

    private static string GetDisplayName(IShellFolder folder, IntPtr pidl, uint flags)
    {
        IntPtr strret = Marshal.AllocCoTaskMem(272);
        try
        {
            Marshal.WriteInt32(strret, 0);
            folder.GetDisplayNameOf(pidl, flags, strret);
            var buffer = new StringBuilder(260);
            _ = StrRetToBuf(strret, pidl, buffer, (uint)buffer.Capacity);
            return buffer.ToString();
        }
        catch
        {
            return "";
        }
        finally
        {
            Marshal.FreeCoTaskMem(strret);
        }
    }

    private static bool NamesMatch(string expected, string actual) =>
        !string.IsNullOrWhiteSpace(actual)
        && actual.Trim().Equals(expected.Trim(), StringComparison.OrdinalIgnoreCase);

    internal static bool LooksLikeParsingName(string value)
    {
        string trimmed = value.Trim();
        return trimmed.StartsWith("::{", StringComparison.Ordinal)
               || trimmed.StartsWith("shell:", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record DesktopChild(string DisplayName, string ParsingName);

    [DllImport("shell32.dll")]
    private static extern int SHGetDesktopFolder(out IShellFolder ppshf);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(
        string pszName,
        IntPtr pbc,
        out IntPtr ppidl,
        uint sfgaoIn,
        out uint psfgaoOut);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr pidl);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, EntryPoint = "StrRetToBufW")]
    private static extern int StrRetToBuf(IntPtr pstr, IntPtr pidl, StringBuilder pszBuf, uint cchBuf);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ShellExecute(
        IntPtr hwnd,
        string lpOperation,
        string lpFile,
        string? lpParameters,
        string? lpDirectory,
        int nShowCmd);

    [ComImport]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        void ParseDisplayName(
            IntPtr hwnd,
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName,
            ref uint pchEaten,
            out IntPtr ppidl,
            ref uint pdwAttributes);

        [PreserveSig]
        int EnumObjects(IntPtr hwnd, uint grfFlags, out IEnumIDList ppenumIDList);

        void BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
        void BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);

        void CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr ppv);
        void GetAttributesOf(uint cidl, IntPtr apidl, ref uint rgfInOut);
        void GetUIObjectOf(
            IntPtr hwndOwner,
            uint cidl,
            IntPtr apidl,
            ref Guid riid,
            IntPtr rgfReserved,
            out IntPtr ppv);

        void GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);
        void SetNameOf(
            IntPtr hwnd,
            IntPtr pidl,
            [MarshalAs(UnmanagedType.LPWStr)] string pszName,
            uint uFlags,
            out IntPtr ppidlOut);
    }

    [ComImport]
    [Guid("000214F2-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumIDList
    {
        [PreserveSig]
        int Next(uint celt, out IntPtr rgelt, out uint pceltFetched);

        [PreserveSig]
        int Skip(uint celt);

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Clone(out IEnumIDList ppenum);
    }
}
