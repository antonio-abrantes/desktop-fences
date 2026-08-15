using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DesktopFences.Native;

/// <summary>
/// Extrai o ícone via Shell (SHGetFileInfo) e devolve PNG.
/// Ficheiros/pastas usam o path; Lixeira / Este computador / Rede usam o PIDL do desktop.
/// </summary>
public static class ShellFileIcon
{
    public static byte[]? ExtractPng(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        string target = PreferShortcutFile(path);
        if (File.Exists(target)
            || Directory.Exists(target)
            || ShellDesktopNamespace.LooksLikeParsingName(target))
        {
            byte[]? fromPath = PngFromPath(target);
            if (fromPath is { Length: > 0 })
                return fromPath;
        }

        return ShellDesktopNamespace.ExtractPng(path);
    }

    public static bool TryExecute(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string target = PreferShortcutFile(path);
        if (File.Exists(target) || Directory.Exists(target))
            return false;

        return ShellDesktopNamespace.TryExecute(path)
               || ShellDesktopNamespace.TryExecute(target);
    }

    internal static byte[]? PngFromPidl(IntPtr pidl)
    {
        if (pidl == IntPtr.Zero)
            return null;

        var info = new NativeMethods.SHFILEINFO();
        NativeMethods.SHGetFileInfo(
            pidl,
            0,
            ref info,
            (uint)Marshal.SizeOf<NativeMethods.SHFILEINFO>(),
            NativeMethods.SHGFI_PIDL
            | NativeMethods.SHGFI_ICON
            | NativeMethods.SHGFI_LARGEICON
            | NativeMethods.SHGFI_ADDOVERLAYS);

        return PngFromIcon(info.hIcon);
    }

    private static byte[]? PngFromPath(string path)
    {
        var info = new NativeMethods.SHFILEINFO();
        NativeMethods.SHGetFileInfo(
            path,
            0,
            ref info,
            (uint)Marshal.SizeOf<NativeMethods.SHFILEINFO>(),
            NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON | NativeMethods.SHGFI_ADDOVERLAYS);

        return PngFromIcon(info.hIcon);
    }

    private static byte[]? PngFromIcon(IntPtr hIcon)
    {
        if (hIcon == IntPtr.Zero)
            return null;

        try
        {
            using Icon fromHandle = Icon.FromHandle(hIcon);
            using Icon clone = (Icon)fromHandle.Clone();
            using Bitmap bitmap = clone.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
        catch
        {
            return null;
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }

    /// <summary>
    /// Pasta e atalho com o mesmo nome visível: o .lnk é o ícone certo.
    /// </summary>
    private static string PreferShortcutFile(string path)
    {
        if (File.Exists(path))
            return path;

        if (!Directory.Exists(path))
            return path;

        string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string? parent = Path.GetDirectoryName(trimmed);
        string name = Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            return path;

        string lnk = Path.Combine(parent, name + ".lnk");
        if (File.Exists(lnk))
            return lnk;

        string url = Path.Combine(parent, name + ".url");
        return File.Exists(url) ? url : path;
    }
}
