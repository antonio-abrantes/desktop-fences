using System.IO;
using DesktopFences.Core;
using DesktopFences.Core.Models;
using DesktopFences.Core.Persistence;

namespace DesktopFences.App;

internal enum StartupPathState
{
    Present,
    Missing,
    Unavailable
}

internal sealed record StartupCustodyReconciliation(
    LayoutDocument Document,
    IReadOnlyList<Guid> RemovedItemIds);

internal static class StartupCustodyReconciler
{
    public static StartupCustodyReconciliation Reconcile(LayoutDocument document) =>
        Reconcile(
            document,
            FenceItemStore.Root(),
            DesktopPaths.FolderList(),
            DesktopPaths.ResolveExisting,
            ProbePath,
            CanEnumerateDirectory);

    internal static StartupCustodyReconciliation Reconcile(
        LayoutDocument document,
        string itemsRoot,
        IReadOnlyList<string> desktopFolders,
        Func<string, string?> resolveExisting,
        Func<string, StartupPathState> probePath,
        Func<string, bool> canEnumerateDirectory)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemsRoot);
        ArgumentNullException.ThrowIfNull(desktopFolders);
        ArgumentNullException.ThrowIfNull(resolveExisting);
        ArgumentNullException.ThrowIfNull(probePath);
        ArgumentNullException.ThrowIfNull(canEnumerateDirectory);

        var inspectionCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        bool CanInspect(string path)
        {
            if (inspectionCache.TryGetValue(path, out bool inspected))
                return inspected;
            inspected = canEnumerateDirectory(path);
            inspectionCache[path] = inspected;
            return inspected;
        }

        if (desktopFolders.Count == 0
            || !CanInspect(itemsRoot)
            || !CanInspect(desktopFolders[0]))
            return new StartupCustodyReconciliation(document, []);

        var missingIds = new HashSet<Guid>();
        foreach (FenceItemState item in document.Fences.SelectMany(fence => fence.Items))
        {
            if (item.Kind != FenceItemKind.Stored
                || item.ItemId == Guid.Empty
                || string.IsNullOrWhiteSpace(item.StorageName)
                || string.IsNullOrWhiteSpace(item.OriginalPath))
                continue;

            string stablePath;
            try { stablePath = FenceItemStore.PayloadPath(itemsRoot, item.ItemId, item.StorageName); }
            catch { continue; }

            StartupPathState stableState = probePath(stablePath);
            if (stableState != StartupPathState.Missing)
                continue;

            string? resolved;
            try { resolved = resolveExisting(item.OriginalPath); }
            catch { continue; }
            if (!string.IsNullOrWhiteSpace(resolved))
                continue;

            StartupPathState originalState = probePath(item.OriginalPath);
            if (originalState != StartupPathState.Missing)
                continue;

            string? originalFolder;
            try { originalFolder = Path.GetDirectoryName(Path.GetFullPath(item.OriginalPath)); }
            catch { continue; }
            string? desktopFolder = desktopFolders.FirstOrDefault(folder => SamePath(folder, originalFolder));
            if (desktopFolder is null || !CanInspect(desktopFolder))
                continue;

            missingIds.Add(item.ItemId);
        }

        if (missingIds.Count == 0)
            return new StartupCustodyReconciliation(document, []);

        LayoutDocument reconciled = LayoutStore.Clone(document);
        foreach (FenceState fence in reconciled.Fences)
            fence.Items.RemoveAll(item => missingIds.Contains(item.ItemId));
        return new StartupCustodyReconciliation(reconciled, missingIds.ToList());
    }

    private static StartupPathState ProbePath(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
            return StartupPathState.Present;
        }
        catch (FileNotFoundException) { return StartupPathState.Missing; }
        catch (DirectoryNotFoundException) { return StartupPathState.Missing; }
        catch (UnauthorizedAccessException) { return StartupPathState.Unavailable; }
        catch (IOException) { return StartupPathState.Unavailable; }
        catch (ArgumentException) { return StartupPathState.Unavailable; }
        catch (NotSupportedException) { return StartupPathState.Unavailable; }
    }

    private static bool CanEnumerateDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                return false;
            using IEnumerator<string> entries = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
            while (entries.MoveNext()) { }
            return true;
        }
        catch { return false; }
    }

    private static bool SamePath(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;
        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
