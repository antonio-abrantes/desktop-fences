using System.IO;
using DesktopFences.Core;
using DesktopFences.Core.Fences;
using DesktopFences.Native;

namespace DesktopFences.App;

internal static class DesktopFenceStubCleaner
{
    public static void TryDelete(string? path)
    {
        IReadOnlyList<string> roots = DesktopPaths.FolderList();
        if (!DesktopFenceStubRules.IsStubPath(path, roots))
            return;

        string full = Path.GetFullPath(path!.Trim().Trim('"'));
        if (!File.Exists(full))
            return;

        TryDeleteOnce(full);
        if (!File.Exists(full))
            return;

        string? parent = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(parent))
            ShellDirectoryNotify.NotifyUpdated(parent);

        TryDeleteOnce(full);
    }

    public static void TryDeleteAllOnDesktop()
    {
        IReadOnlyList<string> roots = DesktopPaths.FolderList();
        foreach (string root in roots)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                continue;

            string[] files;
            try
            {
                files = Directory.GetFiles(root, "*" + DesktopFenceStubRules.Extension);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string file in files)
                TryDelete(file);
        }
    }

    private static void TryDeleteOnce(string full)
    {
        try
        {
            File.Delete(full);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
