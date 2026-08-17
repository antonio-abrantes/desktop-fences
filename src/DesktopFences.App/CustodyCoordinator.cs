using System.IO;
using DesktopFences.Core.Models;
using DesktopFences.Core.Persistence;
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
    private readonly HashSet<Guid> _activeItemIds = [];

    public CustodyCoordinator(LayoutStore layouts, TransactionJournalStore? journals = null)
        : this(
            layouts,
            journals ?? new TransactionJournalStore(),
            new DesktopCustodyBatch(),
            new DesktopCustodyRecoveryActions(),
            NoCustodyFaults.Instance,
            DesktopFences.Core.FenceItemStore.Root())
    {
    }

    internal CustodyCoordinator(
        ILayoutStore layouts,
        ITransactionJournalStore journals,
        IDesktopCustodyBatch batch,
        ICustodyRecoveryActions recoveryActions,
        ICustodyFaultInjector faults,
        string? itemsRoot = null)
    {
        _layouts = layouts;
        _journals = journals;
        _batch = batch;
        _recoveryActions = recoveryActions;
        _faults = faults;
        _itemsRoot = itemsRoot ?? DesktopFences.Core.FenceItemStore.Root();
    }

    public RecoveryReport Recover(LayoutDocument layout) =>
        new TransactionRecovery(_journals, _recoveryActions).Recover(layout);

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
        _batch.FlushShell(moved.Notify);
        return plan.Document;
    }

    public bool CommitInbound(
        LayoutDocument before,
        LayoutDocument after,
        CustodyOperationKind operation,
        IReadOnlyList<DesktopCustodyPlan> plans,
        Action? applyUi,
        out string? error) =>
        Commit(before, after, operation, plans, inbound: true, applyUi, out error);

    public bool CommitOutbound(
        LayoutDocument before,
        LayoutDocument after,
        CustodyOperationKind operation,
        IReadOnlyList<DesktopCustodyPlan> plans,
        Action? applyUi,
        out string? error) =>
        Commit(before, after, operation, plans, inbound: false, applyUi, out error);

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
        out string? error)
    {
        error = null;
        if (!TryAcquire(plans.Select(p => p.ItemId), out List<Guid> acquired))
        {
            error = "Um item já participa de outra operação.";
            return false;
        }
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

            transaction.State = CustodyTransactionState.PayloadChanged;
            _journals.Save(transaction);
            _faults.Hit(CustodyCheckpoint.PayloadChanged, transaction);
            try
            {
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
            _batch.FlushShell(changed.Notify);
            return true;
        }
        finally { Release(acquired); }
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
