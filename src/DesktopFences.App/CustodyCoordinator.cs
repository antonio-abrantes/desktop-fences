using System.IO;
using DesktopFences.Core.Models;
using DesktopFences.Core.Persistence;
using DesktopFences.Core.Recovery;
using DesktopFences.Core.Transactions;
using DesktopFences.Native;

namespace DesktopFences.App;

internal sealed class CustodyCoordinator
{
    private readonly ILayoutStore _layouts;
    private readonly ITransactionJournalStore _journals;
    private readonly IDesktopCustodyBatch _batch;
    private readonly ICustodyRecoveryActions _recoveryActions;
    private readonly ICustodyFaultInjector _faults;
    private readonly string _itemsRoot;
    private readonly DesktopIconService? _desktopPositions;
    private readonly HashSet<Guid> _activeItemIds = [];

    public CustodyCoordinator(LayoutStore layouts, TransactionJournalStore? journals = null)
        : this(
            layouts,
            journals ?? new TransactionJournalStore(),
            new DesktopCustodyBatch(),
            new DesktopCustodyRecoveryActions(),
            NoCustodyFaults.Instance,
            DesktopFences.Core.FenceItemStore.Root(),
            new DesktopIconService())
    {
    }

    internal CustodyCoordinator(
        ILayoutStore layouts,
        ITransactionJournalStore journals,
        IDesktopCustodyBatch batch,
        ICustodyRecoveryActions recoveryActions,
        ICustodyFaultInjector faults,
        string? itemsRoot = null,
        DesktopIconService? desktopPositions = null)
    {
        _layouts = layouts;
        _journals = journals;
        _batch = batch;
        _recoveryActions = recoveryActions;
        _faults = faults;
        _itemsRoot = itemsRoot ?? DesktopFences.Core.FenceItemStore.Root();
        _desktopPositions = desktopPositions;
    }

    public RecoveryReport Recover(LayoutDocument layout, bool preserveDesktop = false)
    {
        DesktopLayoutProtection? protection = null;
        if (_desktopPositions is not null && !preserveDesktop)
        {
            var affected = _journals.LoadAll().SelectMany(journal => journal.Items)
                .SelectMany(item => new[] { item.SourcePath, item.DestinationPath, item.NamespaceKey })
                .OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (affected.Length != 0)
            {
                protection = _desktopPositions.CaptureLayoutProtection(affected);
                preserveDesktop = protection is null;
            }
        }

        try
        {
            return new TransactionRecovery(_journals,
                preserveDesktop ? new DesktopCustodyRecoveryActions(preserveDesktop: true) : _recoveryActions).Recover(layout);
        }
        finally { RestoreDesktopProtection(protection); }
    }

    public void RepairNamespaceKeys(LayoutDocument layout, DesktopRecoverySnapshot? snapshot)
    {
        List<FenceItemState> items = layout.Fences.SelectMany(fence => fence.Items)
            .Where(item => item.Kind == FenceItemKind.Namespace && item.NamespaceKey is null).ToList();
        if (items.Count == 0)
            return;

        var evidence = _journals.LoadAll().SelectMany(transaction => transaction.Items).ToArray();
        bool repaired = false;
        foreach (FenceItemState item in items)
        {
            item.NamespaceKey = DesktopCustodyBatch.ResolveNamespaceKey(item, evidence, snapshot?.Items ?? []);
            repaired |= item.NamespaceKey is not null;
        }
        // Preserve the revision used to determine whether a pending transaction committed.
        if (repaired && layout.Version == LayoutDocument.CurrentVersion)
            _layouts.Save(layout);
    }

    public DesktopSnapshot CaptureDesktop(IDesktopSnapshotSource source) => source.Capture();

    public IReadOnlyList<string> RecordOrphans(LayoutDocument layout)
    {
        IReadOnlyList<string> orphans = OrphanStoreScanner.Find(
            _itemsRoot, layout, _journals.LoadAll());
        string statusPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopFences",
            "recovery-orphans.log");
        if (orphans.Count == 0)
        {
            try { if (File.Exists(statusPath)) File.Delete(statusPath); }
            catch { }
            return orphans;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(statusPath)!);
        File.WriteAllLines(statusPath, orphans);
        return orphans;
    }

    public LayoutDocument MigrateV1(LayoutDocument source)
    {
        LayoutMigrationPlan plan = LayoutV1Migration.Plan(source, _itemsRoot);
        if (plan.Transaction is null)
        {
            plan.Document.Revision = Math.Max(1, source.Revision + 1);
            _layouts.Save(plan.Document);
            return plan.Document;
        }

        CustodyTransaction transaction = plan.Transaction;
        transaction.LayoutRevisionBefore = source.Revision;
        transaction.LayoutRevisionAfter = Math.Max(1, source.Revision + 1);
        plan.Document.Revision = transaction.LayoutRevisionAfter;
        _journals.Save(transaction);
        _faults.Hit(CustodyCheckpoint.Prepared, transaction);
        var moves = transaction.Items.Select(ToPlan).ToList();
        DesktopCustodyBatchResult moved = _batch.ExecuteInbound(moves);
        if (!moved.Success)
        {
            transaction.State = CustodyTransactionState.FailedRecoverable;
            transaction.Error = moved.Error;
            _journals.Save(transaction);
            throw new IOException(moved.Error);
        }
        _faults.Hit(CustodyCheckpoint.PayloadExecuted, transaction);

        transaction.State = CustodyTransactionState.PayloadChanged;
        _journals.Save(transaction);
        _faults.Hit(CustodyCheckpoint.PayloadChanged, transaction);
        try
        {
            _layouts.Save(plan.Document);
        }
        catch (Exception ex)
        {
            bool compensated = _batch.Compensate(moves, wasInbound: true);
            transaction.State = CustodyTransactionState.FailedRecoverable;
            transaction.Error = ex.Message;
            if (compensated)
                _journals.Delete(transaction.OperationId);
            else
                _journals.Save(transaction);
            throw;
        }
        _faults.Hit(CustodyCheckpoint.LayoutSaved, transaction);

        Complete(transaction);
        foreach (string folder in plan.LegacyFolders)
        {
            try { if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder); }
            catch { }
        }
        return plan.Document;
    }

    public bool CommitInbound(
        LayoutDocument before,
        LayoutDocument after,
        CustodyOperationKind operation,
        IReadOnlyList<DesktopCustodyPlan> plans,
        Action? applyUi,
        out string? error,
        bool notifyDesktopDirectory = true) =>
        Commit(before, after, operation, plans, inbound: true, applyUi, out error, notifyDesktopDirectory);

    public bool CommitOutbound(
        LayoutDocument before,
        LayoutDocument after,
        CustodyOperationKind operation,
        IReadOnlyList<DesktopCustodyPlan> plans,
        Action? applyUi,
        out string? error,
        bool notifyDesktopDirectory = true) =>
        Commit(before, after, operation, plans, inbound: false, applyUi, out error, notifyDesktopDirectory);

    public void CommitMetadata(
        LayoutDocument before,
        LayoutDocument after,
        IReadOnlyCollection<Guid>? itemIds = null)
    {
        if (!TryAcquire(itemIds ?? [], out List<Guid> acquired))
            throw new InvalidOperationException("Um item já participa de outra operação.");
        try
        {
            after.Version = LayoutDocument.CurrentVersion;
            after.Revision = Math.Max(before.Revision + 1, after.Revision);
            _layouts.Save(after);
        }
        finally { Release(acquired); }
    }

    public IReadOnlyList<DesktopCustodyPlan> PlanInbound(IEnumerable<DesktopCustodyItem> items) =>
        _batch.PlanInbound(items);

    public IReadOnlyList<DesktopCustodyPlan> PlanOutbound(IEnumerable<DesktopCustodyItem> items) =>
        _batch.PlanOutbound(items);

    private bool Commit(
        LayoutDocument before,
        LayoutDocument after,
        CustodyOperationKind operation,
        IReadOnlyList<DesktopCustodyPlan> plans,
        bool inbound,
        Action? applyUi,
        out string? error,
        bool notifyDesktopDirectory)
    {
        error = null;
        if (!TryAcquire(plans.Select(p => p.ItemId), out List<Guid> acquired))
        {
            error = "Um item já participa de outra operação.";
            return false;
        }
        DesktopLayoutProtection? protection = null;
        bool completed = false;
        bool waitForRemoval = false;
        try
        {
            if (plans.Count == 0)
            {
                after.Version = LayoutDocument.CurrentVersion;
                after.Revision = Math.Max(before.Revision + 1, after.Revision);
                _layouts.Save(after);
                return true;
            }
            after.Version = LayoutDocument.CurrentVersion;
            after.Revision = Math.Max(before.Revision + 1, after.Revision);
            plans = plans.Select(plan => plan is { Kind: FenceItemKind.Stored, SourcePath: { } source }
                ? plan with { SourceIdentity = _recoveryActions.Identity(source) }
                : plan).ToArray();
            if (_desktopPositions is not null)
            {
                string[] affected = plans.Where(plan => plan.Kind == FenceItemKind.Namespace
                        ? plan.NamespaceKey is { } key && (inbound
                            ? !DesktopCustodyBatch.IsNamespaceAlreadyHidden(key) : plan.CanPlaceAfterRelease)
                        : !string.Equals(plan.SourcePath, plan.DestinationPath, StringComparison.OrdinalIgnoreCase))
                    .Select(plan => plan.Kind == FenceItemKind.Namespace ? plan.NamespaceKey
                        : inbound ? plan.SourcePath : plan.DestinationPath)
                    .OfType<string>().ToArray();
                if (affected.Length != 0)
                {
                    protection = _desktopPositions.CaptureLayoutProtection(affected);
                    if (protection is null)
                    {
                        error = "Não foi possível proteger as posições dos ícones existentes no Desktop.";
                        return false;
                    }
                    waitForRemoval = inbound;
                }
            }
            var transaction = new CustodyTransaction
            {
                Operation = operation,
                LayoutRevisionBefore = before.Revision,
                LayoutRevisionAfter = after.Revision,
                Items = plans.Select(plan => ToJournalItem(plan, inbound)).ToList()
            };
            _journals.Save(transaction);
            _faults.Hit(CustodyCheckpoint.Prepared, transaction);

            DesktopCustodyBatchResult changed = inbound
                ? _batch.ExecuteInbound(plans)
                : _batch.ExecuteOutbound(plans);
            if (!changed.Success)
            {
                transaction.State = CustodyTransactionState.FailedRecoverable;
                transaction.Error = changed.Error;
                _journals.Save(transaction);
                error = changed.Error;
                return false;
            }
            _faults.Hit(CustodyCheckpoint.PayloadExecuted, transaction);

            foreach (CustodyTransactionItem item in transaction.Items)
                if (!item.NamespaceItem && item.DestinationPath is { } destination)
                    item.DestinationIdentity = _recoveryActions.Identity(destination);
            transaction.State = CustodyTransactionState.PayloadChanged;
            _journals.Save(transaction);
            _faults.Hit(CustodyCheckpoint.PayloadChanged, transaction);
            try
            {
                RecordCommittedIdentity(after, plans, inbound);
                _layouts.Save(after);
            }
            catch (Exception ex)
            {
                bool compensated = _batch.Compensate(plans, wasInbound: inbound);
                transaction.State = CustodyTransactionState.FailedRecoverable;
                transaction.Error = ex.Message;
                if (compensated)
                    _journals.Delete(transaction.OperationId);
                else
                    _journals.Save(transaction);
                error = ex.Message;
                return false;
            }
            _faults.Hit(CustodyCheckpoint.LayoutSaved, transaction);

            transaction.State = CustodyTransactionState.LayoutCommitted;
            _journals.Save(transaction);
            _faults.Hit(CustodyCheckpoint.LayoutCommitted, transaction);
            try
            {
                applyUi?.Invoke();
            }
            catch (Exception ex)
            {
                transaction.Error = ex.Message;
                _journals.Save(transaction);
                error = ex.Message;
                return false;
            }
            _faults.Hit(CustodyCheckpoint.UiApplied, transaction);
            transaction.State = CustodyTransactionState.Completed;
            _journals.Save(transaction);
            _faults.Hit(CustodyCheckpoint.Completed, transaction);
            _journals.Delete(transaction.OperationId);
            _batch.FlushShell(notifyDesktopDirectory ? changed.Notify : changed.Notify with { UpdateDirectory = false });
            completed = true;
            return true;
        }
        finally
        {
            if (inbound || !completed)
                RestoreDesktopProtection(protection, completed && waitForRemoval);
            Release(acquired);
        }
    }

    private void RestoreDesktopProtection(DesktopLayoutProtection? protection, bool waitForRemoval = false)
    {
        try { _desktopPositions?.PreserveDesktopLayout(protection, waitForRemoval); }
        catch { /* Uma leitura da Shell não altera o resultado da transação de arquivos. */ }
    }

    private static void RecordCommittedIdentity(LayoutDocument layout, IReadOnlyList<DesktopCustodyPlan> plans, bool inbound)
    {
        var byId = plans.ToDictionary(plan => plan.ItemId);
        foreach (FenceItemState item in layout.Fences.SelectMany(fence => fence.Items))
        {
            if (!byId.TryGetValue(item.ItemId, out DesktopCustodyPlan? plan))
                continue;
            if (plan.Kind == FenceItemKind.Namespace)
                item.NamespaceKey = plan.NamespaceKey;
            else
            {
                item.ReleasedPath = inbound ? null : plan.DestinationPath;
                item.ReleasedIdentity = inbound ? null : DesktopFileIdentity.Read(plan.DestinationPath);
            }
        }
    }

    private void Complete(CustodyTransaction transaction)
    {
        transaction.State = CustodyTransactionState.LayoutCommitted;
        _journals.Save(transaction);
        _faults.Hit(CustodyCheckpoint.LayoutCommitted, transaction);
        transaction.State = CustodyTransactionState.Completed;
        _journals.Save(transaction);
        _faults.Hit(CustodyCheckpoint.Completed, transaction);
        _journals.Delete(transaction.OperationId);
    }

    private static CustodyTransactionItem ToJournalItem(DesktopCustodyPlan plan, bool inbound) => new()
    {
        ItemId = plan.ItemId,
        Name = plan.Name,
        SourcePath = plan.SourcePath,
        DestinationPath = plan.DestinationPath,
        SourceIdentity = plan.SourceIdentity,
        NamespaceItem = plan.Kind == FenceItemKind.Namespace,
        NamespaceKey = plan.NamespaceKey,
        DestinationNamespaceHidden = inbound
    };

    private static DesktopCustodyPlan ToPlan(CustodyTransactionItem item) => new(
        item.ItemId,
        item.NamespaceItem ? FenceItemKind.Namespace : FenceItemKind.Stored,
        item.Name,
        item.SourcePath,
        item.DestinationPath,
        item.SourcePath,
        Path.GetFileName(item.DestinationPath),
        item.NamespaceKey);

    private bool TryAcquire(IEnumerable<Guid> itemIds, out List<Guid> acquired)
    {
        acquired = itemIds.Distinct().ToList();
        lock (_activeItemIds)
        {
            if (acquired.Any(_activeItemIds.Contains))
                return false;
            foreach (Guid id in acquired)
                _activeItemIds.Add(id);
            return true;
        }
    }

    private void Release(IEnumerable<Guid> itemIds)
    {
        lock (_activeItemIds)
        {
            foreach (Guid id in itemIds)
                _activeItemIds.Remove(id);
        }
    }
}
