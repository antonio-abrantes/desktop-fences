using DesktopFences.Core;
using DesktopFences.Core.Models;
using DesktopFences.Core.Occupancy;
using DesktopFences.Core.Transactions;

namespace DesktopFences.Native;

public sealed record DesktopCustodyItem(
    Guid ItemId,
    FenceItemKind Kind,
    string Name,
    string? RuntimePath,
    string? OriginalPath,
    string? StorageName);

public sealed record DesktopCustodyPlan(
    Guid ItemId,
    FenceItemKind Kind,
    string Name,
    string? SourcePath,
    string? DestinationPath,
    string? OriginalPath,
    string? StorageName,
    string? NamespaceKey);

public sealed record DesktopCustodyBatchResult(
    bool Success,
    IReadOnlyList<DesktopCustodyPlan> Applied,
    string? Error)
{
    public static DesktopCustodyBatchResult Failed(string error) => new(false, [], error);
}

/// <summary>Planeia o lote inteiro antes de tocar no Desktop e compensa falhas parciais.</summary>
public interface IDesktopCustodyBatch
{
    IReadOnlyList<DesktopCustodyPlan> PlanInbound(IEnumerable<DesktopCustodyItem> items);
    IReadOnlyList<DesktopCustodyPlan> PlanOutbound(IEnumerable<DesktopCustodyItem> items);
    DesktopCustodyBatchResult ExecuteInbound(IReadOnlyList<DesktopCustodyPlan> plans);
    DesktopCustodyBatchResult ExecuteOutbound(IReadOnlyList<DesktopCustodyPlan> plans);
    bool Compensate(IReadOnlyList<DesktopCustodyPlan> plans, bool wasInbound);
    void FlushShell();
}

public sealed class DesktopCustodyBatch : IDesktopCustodyBatch
{
    public IReadOnlyList<DesktopCustodyPlan> PlanInbound(IEnumerable<DesktopCustodyItem> items)
    {
        IReadOnlyList<string> desktopFolders = DesktopPaths.FolderList();
        var result = new List<DesktopCustodyPlan>();
        foreach (DesktopCustodyItem item in items)
        {
            if (item.Kind == FenceItemKind.Stored && !string.IsNullOrWhiteSpace(item.StorageName))
            {
                string stableDestination = FenceItemStore.PayloadPath(item.ItemId, item.StorageName);
                if (File.Exists(stableDestination) || Directory.Exists(stableDestination))
                {
                    if (!string.IsNullOrWhiteSpace(item.OriginalPath)
                        && Exists(item.OriginalPath)
                        && !SamePath(item.OriginalPath, stableDestination))
                        throw new InvalidOperationException(
                            $"Há duas cópias físicas para o mesmo ItemId: {item.Name}");
                    result.Add(new DesktopCustodyPlan(
                        item.ItemId, item.Kind, item.Name,
                        stableDestination, stableDestination, item.OriginalPath,
                        item.StorageName, null));
                    continue;
                }
            }

            if (item.Kind == FenceItemKind.Namespace)
            {
                string key = ShellDesktopNamespace.GetParsingName(item.RuntimePath ?? item.OriginalPath ?? item.Name)
                             ?? item.RuntimePath ?? item.OriginalPath ?? item.Name;
                result.Add(new DesktopCustodyPlan(
                    item.ItemId, item.Kind, item.Name, null, null, null, null, key));
                continue;
            }

            string? resolved = DesktopPaths.ResolveExisting(item.RuntimePath ?? item.Name);
            if (resolved is not null && !desktopFolders.Any(folder => IsUnder(folder, resolved)))
                throw new InvalidOperationException($"O item não pertence ao Desktop: {item.Name}");
            string? parsing = resolved is null
                ? ShellDesktopNamespace.GetParsingName(item.RuntimePath ?? item.Name)
                : null;
            DesktopHidePlan hide = DesktopHide.For(resolved, desktopFolders, parsing);
            if (hide.Kind == DesktopHideKind.None)
                throw new InvalidOperationException($"O item não pertence ao Desktop: {item.Name}");

            if (hide.Kind == DesktopHideKind.NamespaceIcon)
            {
                result.Add(new DesktopCustodyPlan(
                    item.ItemId, FenceItemKind.Namespace, item.Name,
                    null, null, null, null, hide.Key));
                continue;
            }

            string storageName = string.IsNullOrWhiteSpace(item.StorageName)
                ? Path.GetFileName(hide.Key)
                : item.StorageName;
            if (string.IsNullOrWhiteSpace(storageName))
                throw new InvalidOperationException($"Não foi possível determinar o nome de armazenamento: {item.Name}");
            string destination = FenceItemStore.PayloadPath(item.ItemId, storageName);
            result.Add(new DesktopCustodyPlan(
                item.ItemId, FenceItemKind.Stored, item.Name,
                hide.Key, destination, item.OriginalPath ?? hide.Key, storageName, null));
        }

        EnsureUnique(result);
        return result;
    }

    public IReadOnlyList<DesktopCustodyPlan> PlanOutbound(IEnumerable<DesktopCustodyItem> items)
    {
        IReadOnlyList<string> desktopFolders = DesktopPaths.FolderList();
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<DesktopCustodyPlan>();
        foreach (DesktopCustodyItem item in items)
        {
            if (item.Kind == FenceItemKind.Namespace)
            {
                string key = item.RuntimePath ?? item.OriginalPath ?? item.Name;
                result.Add(new DesktopCustodyPlan(
                    item.ItemId, item.Kind, item.Name, null, null, null, null, key));
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.StorageName))
                throw new InvalidOperationException($"Item armazenado sem StorageName: {item.Name}");
            string source = FenceItemStore.PayloadPath(item.ItemId, item.StorageName);
            if (!Exists(source)
                && !string.IsNullOrWhiteSpace(item.RuntimePath)
                && Exists(item.RuntimePath)
                && !FenceItemStore.IsUnderRoot(item.RuntimePath))
            {
                if (!desktopFolders.Any(folder => IsUnder(folder, item.RuntimePath)))
                    throw new InvalidOperationException($"O item não pertence ao Desktop: {item.Name}");
                result.Add(new DesktopCustodyPlan(
                    item.ItemId, item.Kind, item.Name, item.RuntimePath, item.RuntimePath,
                    item.OriginalPath, item.StorageName, null));
                continue;
            }
            string folder = FenceItemStore.RestoreDirectory(item.OriginalPath, desktopFolders);
            string? fileName = Path.GetFileName(item.OriginalPath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = item.StorageName;
            string destination = ReserveUnique(folder, fileName, reserved);
            result.Add(new DesktopCustodyPlan(
                item.ItemId, item.Kind, item.Name, source, destination,
                item.OriginalPath, item.StorageName, null));
        }

        EnsureUnique(result);
        return result;
    }

    public DesktopCustodyBatchResult ExecuteInbound(IReadOnlyList<DesktopCustodyPlan> plans) =>
        Execute(plans, forward: true, hideNamespace: true);

    public DesktopCustodyBatchResult ExecuteOutbound(IReadOnlyList<DesktopCustodyPlan> plans) =>
        Execute(plans, forward: true, hideNamespace: false);

    public bool Compensate(IReadOnlyList<DesktopCustodyPlan> plans, bool wasInbound)
    {
        DesktopCustodyBatchResult result = Execute(
            plans.Reverse().ToList(), forward: false, hideNamespace: !wasInbound);
        return result.Success;
    }

    public void FlushShell()
    {
        foreach (string folder in DesktopPaths.FolderList())
        {
            if (!Directory.Exists(folder))
                continue;
            NativeMethods.SHChangeNotify(
                NativeMethods.SHCNE_UPDATEDIR,
                NativeMethods.SHCNF_PATHW | NativeMethods.SHCNF_FLUSHNOWAIT,
                folder,
                IntPtr.Zero);
        }

        NativeMethods.SHChangeNotify(
            NativeMethods.SHCNE_ASSOCCHANGED,
            NativeMethods.SHCNF_IDLIST | NativeMethods.SHCNF_FLUSHNOWAIT,
            IntPtr.Zero,
            IntPtr.Zero);
    }

    private static DesktopCustodyBatchResult Execute(
        IReadOnlyList<DesktopCustodyPlan> plans,
        bool forward,
        bool hideNamespace)
    {
        bool Apply(DesktopCustodyPlan plan)
        {
            if (plan.Kind == FenceItemKind.Namespace)
                return !string.IsNullOrWhiteSpace(plan.NamespaceKey)
                       && DesktopVisibility.SetNamespaceHidden(plan.NamespaceKey, hideNamespace);
            string? source = forward ? plan.SourcePath : plan.DestinationPath;
            string? destination = forward ? plan.DestinationPath : plan.SourcePath;
            return !string.IsNullOrWhiteSpace(source)
                   && !string.IsNullOrWhiteSpace(destination)
                   && MoveExact(source, destination);
        }

        bool Undo(DesktopCustodyPlan plan)
        {
            if (plan.Kind == FenceItemKind.Namespace)
                return DesktopVisibility.SetNamespaceHidden(plan.NamespaceKey!, !hideNamespace);
            string from = forward ? plan.DestinationPath! : plan.SourcePath!;
            string to = forward ? plan.SourcePath! : plan.DestinationPath!;
            return MoveExact(from, to);
        }

        CompensatingBatchResult<DesktopCustodyPlan> result =
            CompensatingBatch.Execute(plans, Apply, Undo);
        if (result.Success)
            return new DesktopCustodyBatchResult(true, result.Applied, null);
        string suffix = result.CompensationComplete
            ? " O lote foi compensado."
            : " A compensação ficou incompleta.";
        return new DesktopCustodyBatchResult(
            false,
            result.Applied,
            $"Falha ao mover {result.FailedItem?.Name ?? "item"}.{suffix}");
    }

    internal static bool MoveExact(string source, string destination)
    {
        if (SamePath(source, destination))
            return Exists(source);
        bool sourceExists = Exists(source);
        bool destinationExists = Exists(destination);
        if (!sourceExists)
            return destinationExists;
        if (destinationExists)
            return false;
        string? folder = Path.GetDirectoryName(destination);
        if (string.IsNullOrWhiteSpace(folder))
            return false;
        return ShellFileMove.Move(source, folder, Path.GetFileName(destination));
    }

    private static string ReserveUnique(string folder, string fileName, HashSet<string> reserved)
    {
        string candidate = FenceItemStore.UniqueDestination(folder, fileName);
        if (reserved.Add(candidate))
            return candidate;
        string stem = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        for (int n = 2; n < 10_000; n++)
        {
            candidate = Path.Combine(folder, $"{stem} ({n}){ext}");
            if (!Exists(candidate) && reserved.Add(candidate))
                return candidate;
        }
        candidate = Path.Combine(folder, $"{stem}-{Guid.NewGuid():N}{ext}");
        reserved.Add(candidate);
        return candidate;
    }

    private static void EnsureUnique(IReadOnlyList<DesktopCustodyPlan> plans)
    {
        if (plans.Select(p => p.ItemId).Distinct().Count() != plans.Count)
            throw new InvalidOperationException("O lote contém ItemId duplicado.");
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    private static bool SamePath(string a, string b)
    {
        try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
    }

    private static bool IsUnder(string root, string path)
    {
        try
        {
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
                                    + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}

public sealed class DesktopCustodyRecoveryActions : ICustodyRecoveryActions
{
    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    public bool Move(string source, string destination) => DesktopCustodyBatch.MoveExact(source, destination);

    public bool SetNamespaceHidden(string key, bool hidden) =>
        DesktopVisibility.SetNamespaceHidden(key, hidden);
}
