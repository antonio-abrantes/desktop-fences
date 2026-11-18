using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DesktopFences.Native;

public static class DesktopFileIdentity
{
    public static string? Read(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            return null;

        const uint openExisting = 3;
        const uint shareReadWriteDelete = 7;
        const uint openDirectoryOrLink = 0x02200000;
        using SafeFileHandle handle = CreateFile(
            path, 0, shareReadWriteDelete, IntPtr.Zero, openExisting, openDirectoryOrLink, IntPtr.Zero);
        return !handle.IsInvalid && GetFileInformationByHandleEx(handle, 18, out FileIdInfo info, 24)
            ? $"{info.Volume:X16}:{info.IdHigh:X16}{info.IdLow:X16}"
            : null;
    }

    public static bool Matches(string? path, string? identity) =>
        identity is not null && string.Equals(Read(path), identity, StringComparison.Ordinal);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileIdInfo
    {
        public ulong Volume;
        public ulong IdLow;
        public ulong IdHigh;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle file, int infoClass, out FileIdInfo info, uint bytes);
}
