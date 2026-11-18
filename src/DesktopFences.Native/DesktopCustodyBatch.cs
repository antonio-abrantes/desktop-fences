using DesktopFences.Core;
using DesktopFences.Core.Models;
using DesktopFences.Core.Occupancy;
using DesktopFences.Core.Recovery;
using DesktopFences.Core.Transactions;

namespace DesktopFences.Native;

public sealed record DesktopCustodyItem(
    Guid ItemId,
    FenceItemKind Kind,
    string Name,
    string? RuntimePath,
    string? OriginalPath,
    string? StorageName,
    string? NamespaceKey = null,
    string? ReleasedPath = null,
    string? ReleasedIdentity = null)
{
    public static DesktopCustodyItem FromState(FenceItemState item) => new(
        item.ItemId, item.Kind, item.Name, item.ReleasedPath ?? item.OriginalPath ?? item.Name,
        item.OriginalPath, item.StorageName, item.NamespaceKey, item.ReleasedPath, item.ReleasedIdentity);
}

public sealed record DesktopCustodyPlan(
    Guid ItemId,
    FenceItemKind Kind,
    string Name,
    string? SourcePath,
    string? DestinationPath,
    string? OriginalPath,
    string? StorageName,
    string? NamespaceKey,
    bool RevealsNamespace = true,
    string? SourceIdentity = null)
{
    public bool CanPlaceAfterRelease => Kind == FenceItemKind.Namespace
        ? RevealsNamespace
        : !string.Equals(SourcePath, DestinationPath, StringComparison.OrdinalIgnoreCase);
}

public readonly record struct ShellNotifyRequest(bool UpdateDirectory, bool AssocChanged)
{
    public static ShellNotifyRequest None => default;
    public static ShellNotifyRequest Full => new(true, true);
    public bool Any => UpdateDirectory || AssocChanged;
}

public sealed record DesktopCustodyBatchResult(
    bool Success,
    IReadOnlyList<DesktopCustodyPlan> Applied,
    string? Error,
    ShellNotifyRequest Notify = default)
{
    public static DesktopCustodyBatchResult Failed(string error) =>
        new(false, [], error, ShellNotifyRequest.None);
}

/// <summary>Planeia o lote inteiro antes de tocar no Desktop e compensa falhas parciais.</summary>
public interface IDesktopCustodyBatch
{
    IReadOnlyList<DesktopCustodyPlan> PlanInbound(IEnumerable<DesktopCustodyItem> items);
    IReadOnlyList<DesktopCustodyPlan> PlanOutbound(IEnumerable<DesktopCustodyItem> items);
    DesktopCustodyBatchResult ExecuteInbound(IReadOnlyList<DesktopCustodyPlan> plans);
    DesktopCustodyBatchResult ExecuteOutbound(IReadOnlyList<DesktopCustodyPlan> plans);
    bool Compensate(IReadOnlyList<DesktopCustodyPlan> plans, bool wasInbound);
    void FlushShell(ShellNotifyRequest notify);
}

public sealed class DesktopCustodyBatch : IDesktopCustodyBatch
{
    public static bool IsNamespaceAlreadyHidden(string key) =>
        DesktopVisibility.IsNamespaceStateSet(key, hidden: true);

    public static string? GetDropNamespaceKey(string value)
    {
        string? key = ShellDesktopNamespace.ResolveKey(value);
        return key is not null && string.Equals(DesktopHide.ToShellParsingName(key), value, StringComparison.OrdinalIgnoreCase)
            ? key : null;
    }

    public static bool IsEligibleDropItem(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (Path.IsPathFullyQualified(value)
            ? DesktopPaths.FolderList().Any(folder => IsUnder(folder, value))
            : GetDropNamespaceKey(value) is not null);

    public static string GetDropDisplayName(string value) =>
        GetDropNamespaceKey(value) is string key
            ? ShellDesktopNamespace.GetNamespaceLabel(key) ?? value
            : DesktopPaths.VisibleName(value);
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
                    result.Add(new DesktopCustodyPlan(
                        item.ItemId, item.Kind, item.Name,
                        stableDestination, stableDestination, item.OriginalPath,
                        item.StorageName, null));
                    continue;
                }
            }

            if (item.Kind == FenceItemKind.Namespace)
            {
                string key = ShellDesktopNamespace.RequireKey(item);
                result.Add(new DesktopCustodyPlan(
                    item.ItemId, item.Kind, item.Name, null, null, null, null, key));
                continue;
            }

            string? source = item.RuntimePath ?? item.Name;
            string? resolved = item.ReleasedPath is not null
                ? DesktopFileIdentity.Matches(item.ReleasedPath, item.ReleasedIdentity) ? item.ReleasedPath : null
                : Path.IsPathFullyQualified(source)
                    ? Exists(source) ? source : null
                    : DesktopPaths.ResolveExisting(source);
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
                string key = ShellDesktopNamespace.RequireKey(item);
                result.Add(new DesktopCustodyPlan(
                    item.ItemId, item.Kind, item.Name, null, null, null, null, key, IsNamespaceAlreadyHidden(key)));
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.StorageName))
                throw new InvalidOperationException($"Item armazenado sem StorageName: {item.Name}");
            string source = FenceItemStore.PayloadPath(item.ItemId, item.StorageName);
            if (!Exists(source))
            {
                if (item.ReleasedPath is null || !desktopFolders.Any(folder => IsUnder(folder, item.ReleasedPath))
                    || !DesktopFileIdentity.Matches(item.ReleasedPath, item.ReleasedIdentity))
                    throw new InvalidOperationException($"Não foi possível confirmar o arquivo no armazenamento ou no Desktop: {item.Name}");
                result.Add(new DesktopCustodyPlan(
                    item.ItemId, item.Kind, item.Name, item.ReleasedPath, item.ReleasedPath,
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

    public static string? ResolveNamespaceKey(
        FenceItemState item,
        IEnumerable<CustodyTransactionItem> journal,
        IEnumerable<DesktopRecoveryItem> snapshot)
    {
        string? explicitKey = ShellDesktopNamespace.ResolveKey(item.NamespaceKey, item.OriginalPath, item.Path);
        if (explicitKey is not null)
            return explicitKey;

        string[] keys = journal.Where(entry => entry.NamespaceItem && entry.ItemId == item.ItemId)
            .Select(entry => ShellDesktopNamespace.ResolveKey(entry.NamespaceKey))
            .Concat(snapshot.Where(entry => entry.Kind == FenceItemKind.Namespace && entry.ItemId == item.ItemId)
                .Select(entry => ShellDesktopNamespace.ResolveKey(entry.NamespaceKey, entry.OriginalPath)))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return keys.Length == 0 ? ShellDesktopNamespace.ResolveDisplayName(item.Name)
            : keys.Length == 1 ? keys[0] : null;
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

    public void FlushShell(ShellNotifyRequest notify)
        => FlushShell(notify, DesktopPaths.FolderList());

    internal static void FlushShell(ShellNotifyRequest notify, IEnumerable<string> folders)
    {
        if (!notify.Any)
            return;

        if (notify.UpdateDirectory)
        {
            foreach (string folder in folders)
            {
                if (!Directory.Exists(folder))
                    continue;
                NativeMethods.SHChangeNotify(
                    NativeMethods.SHCNE_UPDATEDIR,
                    NativeMethods.SHCNF_PATHW | NativeMethods.SHCNF_FLUSHNOWAIT,
                    folder,
                    IntPtr.Zero);
            }
        }

        if (notify.AssocChanged)
        {
            NativeMethods.SHChangeNotify(
                NativeMethods.SHCNE_ASSOCCHANGED,
                NativeMethods.SHCNF_IDLIST | NativeMethods.SHCNF_FLUSHNOWAIT,
                IntPtr.Zero,
                IntPtr.Zero);
        }
    }

    private static DesktopCustodyBatchResult Execute(
        IReadOnlyList<DesktopCustodyPlan> plans,
        bool forward,
        bool hideNamespace)
    {
        bool notifyDirectory = false;
        bool notifyAssoc = false;

        bool Apply(DesktopCustodyPlan plan)
        {
            if (plan.Kind == FenceItemKind.Namespace)
            {
                if (string.IsNullOrWhiteSpace(plan.NamespaceKey))
                    return false;
                if (!DesktopVisibility.SetNamespaceHidden(
                        plan.NamespaceKey, hideNamespace, out bool changed))
                    return false;
                if (changed)
                    notifyAssoc = true;
                return true;
            }

            string? source = forward ? plan.SourcePath : plan.DestinationPath;
            string? destination = forward ? plan.DestinationPath : plan.SourcePath;
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination))
                return false;
            if (forward && plan.SourceIdentity is not null && !DesktopFileIdentity.Matches(source, plan.SourceIdentity))
                return false;
            if (!MoveExact(source, destination, out bool moved))
                return false;
            if (moved)
                notifyDirectory = true;
            return true;
        }

        bool Undo(DesktopCustodyPlan plan)
        {
            if (plan.Kind == FenceItemKind.Namespace)
                return DesktopVisibility.SetNamespaceHidden(plan.NamespaceKey!, !hideNamespace);
            string from = forward ? plan.DestinationPath! : plan.SourcePath!;
            string to = forward ? plan.SourcePath! : plan.DestinationPath!;
            return MoveExact(from, to, out _);
        }

        CompensatingBatchResult<DesktopCustodyPlan> result =
            CompensatingBatch.Execute(plans, Apply, Undo);
        var notify = new ShellNotifyRequest(notifyDirectory, notifyAssoc);
        if (result.Success)
            return new DesktopCustodyBatchResult(true, result.Applied, null, notify);
        string suffix = result.CompensationComplete
            ? " O lote foi compensado."
            : " A compensação ficou incompleta.";
        return new DesktopCustodyBatchResult(
            false,
            result.Applied,
            $"Falha ao restaurar ou mover {result.FailedItem?.Name ?? "item"}.{suffix}",
            ShellNotifyRequest.None);
    }

    internal static bool MoveExact(string source, string destination) =>
        MoveExact(source, destination, out _);

    internal static bool MoveExact(string source, string destination, out bool moved)
    {
        moved = false;
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
        moved = ShellFileMove.Move(source, folder, Path.GetFileName(destination));
        return moved;
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

public sealed class DesktopCustodyRecoveryActions(bool preserveDesktop = false) : ICustodyRecoveryActions
{
    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
    public string? Identity(string path) => DesktopFileIdentity.Read(path);

    public bool Move(string source, string destination)
    {
        if (preserveDesktop)
        {
            IReadOnlyList<string> folders = DesktopPaths.FolderList();
            if (DesktopHide.IsUnderDesktopFolders(source, folders) || DesktopHide.IsUnderDesktopFolders(destination, folders))
                return false;
        }
        return DesktopCustodyBatch.MoveExact(source, destination);
    }

    public bool SetNamespaceHidden(string key, bool hidden) =>
        preserveDesktop ? DesktopVisibility.IsNamespaceStateSet(key, hidden)
            : DesktopVisibility.SetNamespaceHidden(key, hidden);
}
