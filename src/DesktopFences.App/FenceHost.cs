using System.IO;
using System.Windows.Threading;
using DesktopFences.App.Localization;
using DesktopFences.App.ViewModels;
using DesktopFences.Core;
using DesktopFences.Core.Fences;
using DesktopFences.Core.Models;
using DesktopFences.Core.Occupancy;
using DesktopFences.Core.Persistence;
using DesktopFences.Core.Recovery;
using DesktopFences.Core.Transactions;
using DesktopFences.Native;

namespace DesktopFences.App;

public sealed class FenceHost
{
    private readonly LayoutStore _store = new();
    private readonly CustodyCoordinator _custody;
    private readonly List<FenceWindow> _windows = [];
    private readonly ExplorerListViewGuard _explorer = new();
    private readonly DesktopIconService _recoveryIcons = new();
    private readonly DesktopRecoverySnapshotStore _recoverySnapshots = new();
    private DispatcherTimer? _explorerWatch;
    private bool _saving;
    private bool _paused;
    private FenceWindow? _itemDragSource;
    private bool _blockDesktopInbound;
    private int _returningItemsToDesktop;
    private long _layoutRevision;
    private bool _payloadReleased;
    private bool _leaveCustodyInStore;
    private TitleAlignment? _defaultTitleAlignment;
    private FenceTheme? _defaultTheme;

    public FenceHost()
    {
        _custody = new CustodyCoordinator(_store);
    }

    public event Action? FencesChanged;

    public bool IsPaused => _paused;

    public string UiLanguage { get; private set; } = UiLanguageCodes.System;

    public IReadOnlyList<FenceWindow> Windows => _windows;

    public bool IsBlockingDesktopInbound => _blockDesktopInbound;

    public bool IsReturningItemsToDesktop => _returningItemsToDesktop > 0;

    public bool BlocksDesktopInbound => _blockDesktopInbound || IsReturningItemsToDesktop;
    public string? LastCustodyError { get; private set; }
    public IReadOnlyList<string> StartupWarnings { get; private set; } = [];

    internal const int ReturnToDesktopMarginMs = 250;

    internal DesktopSnapshot CaptureDesktop(IDesktopSnapshotSource source) =>
        _custody.CaptureDesktop(source);

    public int Start()
    {
        LayoutDocument doc = _store.LoadOrEmpty();
        _custody.RepairNamespaceKeys(doc, _recoverySnapshots.Load());
        // O estado que o usuário vê no Desktop é capturado antes de qualquer
        // reconciliação capaz de mover payload ou alterar posições na Shell.
        SaveRecoverySnapshot(doc);
        RecoveryReport recovery = _custody.Recover(doc, preserveDesktop: !_recoveryIcons.IsManualArrangement());
        if (!recovery.Complete)
            throw new InvalidDataException(string.Join(Environment.NewLine, recovery.Errors));
        if (doc.Version == 1)
        {
            doc = _custody.MigrateV1(doc);
            _custody.RepairNamespaceKeys(doc, _recoverySnapshots.Load());
        }
        doc = EnsureCustodyBeforeUi(doc);
        _custody.RecordOrphans(doc);
        _layoutRevision = doc.Revision;
        UiLanguage = UiLanguageCodes.Normalize(doc.UiLanguage);
        _defaultTitleAlignment = doc.DefaultTitleAlignment;
        _defaultTheme = doc.DefaultTheme?.Normalized();
        UiLocale.Apply(UiLanguage);
        int existingCount = doc.Fences.Count;
        FenceLayoutRules.EnsureAtLeastOne(
            doc.Fences,
            Loc.T("DefaultFenceTitle"),
            _defaultTitleAlignment,
            _defaultTheme);
        foreach (FenceState state in doc.Fences)
            Spawn(state);

        SaveAll();
        FencesChanged?.Invoke();
        _explorer.Arm();
        _explorerWatch = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _explorerWatch.Tick += OnExplorerWatch;
        _explorerWatch.Start();
        return existingCount;
    }

    private void SaveRecoverySnapshot(LayoutDocument document)
    {
        DesktopRecoverySnapshot? previous = _recoverySnapshots.Load();
        DesktopSnapshot visible = _recoveryIcons.Capture();
        if (!visible.Connected && previous is null)
            throw new InvalidDataException(
                "Não foi possível criar o snapshot de segurança das posições do Desktop.");

        DesktopRecoverySnapshot snapshot = DesktopRecoverySnapshotBuilder.Build(
            visible.Icons,
            document,
            DesktopPaths.ResolveExisting,
            previous);
        _recoverySnapshots.Save(snapshot);
    }

    private void OnExplorerWatch(object? sender, EventArgs e)
    {
        if (_paused)
            return;

        foreach (FenceWindow window in _windows)
        {
            try { window.EnsureDesktopSurvival(); }
            catch { /* Win+D não pode deixar uma fence a bloquear as outras */ }
        }

        if (!_explorer.TryConsumeReconnect())
            return;

        foreach (FenceWindow window in _windows)
        {
            try { window.RebindAfterExplorer(); }
            catch { /* uma fence não pode impedir o re-hide das outras */ }
        }
    }

    public void SetUiLanguage(string? code)
    {
        string next = UiLanguageCodes.Normalize(code);
        if (next == UiLanguage)
            return;

        UiLanguage = next;
        UiLocale.Apply(next);
        SaveAll();
    }

    public bool PauseAll()
    {
        if (_returningItemsToDesktop > 0)
            return false;
        if (_paused)
            return true;
        LastCustodyError = null;
        if (!ReleaseAll(CustodyOperationKind.Pause))
            return false;
        _paused = true;
        _payloadReleased = true;
        foreach (FenceWindow window in _windows)
            window.PauseVisual();
        return true;
    }

    public bool ResumeAll()
    {
        if (!_paused)
            return true;
        if (!ConcealAll(CustodyOperationKind.Resume))
            return false;
        _paused = false;
        _payloadReleased = false;
        foreach (FenceWindow window in _windows)
            window.ResumeVisual();
        return true;
    }

    public void RestoreAllIcons()
    {
        if (_leaveCustodyInStore)
            return;
        if (!_payloadReleased && ReleaseAll(CustodyOperationKind.Shutdown))
            _payloadReleased = true;
    }

    public bool PrepareExit() => PrepareExitCore(releaseCustody: true);

    internal bool PrepareUserExit() => PrepareExitCore(releaseCustody: true, allowDeferred: true);

    public bool PrepareUpgradeExit() => PrepareExitCore(releaseCustody: false);

    private bool PrepareExitCore(bool releaseCustody, bool allowDeferred = false)
    {
        LastCustodyError = null;
        if (_returningItemsToDesktop > 0)
        {
            LastCustodyError = Loc.T("CustodyOperationInProgress");
            return false;
        }

        SaveAll();
        if (releaseCustody && !_payloadReleased && !ReleaseAll(CustodyOperationKind.Shutdown, allowDeferred))
            return false;

        if (_explorerWatch is not null)
        {
            _explorerWatch.Stop();
            _explorerWatch.Tick -= OnExplorerWatch;
            _explorerWatch = null;
        }

        if (releaseCustody)
            _payloadReleased = true;
        else
            _leaveCustodyInStore = true;

        foreach (FenceWindow window in _windows.ToList())
        {
            window.LayoutChanged -= OnWindowLayoutChanged;
            window.SuppressPersistOnClose = true;
            window.Close();
        }

        _windows.Clear();
        return true;
    }

    public bool TryAddNew()
    {
        List<FenceState> current = _windows.Select(w => w.CaptureState()).ToList();
        FenceState state = FenceLayoutRules.PlaceNew(
            current,
            Loc.T("DefaultFenceTitle"),
            _defaultTitleAlignment,
            _defaultTheme);
        FenceWindow window = Spawn(state);
        if (_paused)
            window.PauseVisual();
        SaveAll();
        FencesChanged?.Invoke();
        return true;
    }

    public async Task<bool> TryRemoveAsync(Guid id)
    {
        if (_returningItemsToDesktop > 0)
            return false;

        if (!FenceLayoutRules.CanRemove(_windows.Count))
            return false;

        FenceWindow? window = _windows.FirstOrDefault(w => w.FenceId == id);
        if (window is null)
            return false;

        LayoutDocument before = CaptureDocument();
        List<FenceItemVm> items = window.Items.ToList();
        LayoutDocument after = LayoutStore.Clone(before);
        after.Fences.RemoveAll(f => f.Id == id);
        IReadOnlyList<DesktopCustodyPlan> plans;
        try { plans = _custody.PlanOutbound(items.Select(ToCustodyItem)); }
        catch { return false; }
        var states = items.Select(item => item.ToState()).ToList();
        var snapshot = _recoverySnapshots.Load();
        var reservation = _recoveryIcons.ReserveReturnPositions(BuildReleasedPlacements(states, plans, snapshot));
        if (plans.Any(plan => !CanReturn(plan, reservation)))
            return false;

        bool restoreBarrier = items.Count > 0;
        if (restoreBarrier)
            Interlocked.Increment(ref _returningItemsToDesktop);

        try
        {
            if (!_custody.CommitOutbound(
                    before, after, CustodyOperationKind.RemoveFence, plans,
                    () => window.ApplyOutbound(items, plans), out _, notifyDesktopDirectory: false))
                return false;

            _layoutRevision = after.Revision;

            if (restoreBarrier)
            {
                PlaceReleasedItems(reservation,
                    BuildReleasedPlacements(items.Select(item => item.ToState()).ToList(), plans, snapshot));
                await Task.Delay(ReturnToDesktopMarginMs).ConfigureAwait(true);
            }

            window.LayoutChanged -= OnWindowLayoutChanged;
            window.SuppressPersistOnClose = true;
            _windows.Remove(window);
            if (System.Windows.Application.Current.MainWindow == window
                || System.Windows.Application.Current.MainWindow is null)
                System.Windows.Application.Current.MainWindow = _windows.FirstOrDefault();
            window.Close();
            FencesChanged?.Invoke();
            return true;
        }
        finally
        {
            if (restoreBarrier)
                Interlocked.Decrement(ref _returningItemsToDesktop);
        }
    }

    public void SetTitleAlignment(Guid id, TitleAlignment alignment)
    {
        FenceWindow? window = _windows.FirstOrDefault(w => w.FenceId == id);
        if (window is null)
            return;

        window.SetTitleAlignment(alignment);
        FencesChanged?.Invoke();
    }

    public void SetTitleAlignmentAll(TitleAlignment alignment)
    {
        foreach (FenceWindow window in _windows)
            window.SetTitleAlignment(alignment, persist: false);
        SaveAll();
        FencesChanged?.Invoke();
    }

    public void SetTheme(Guid id, FenceTheme theme)
    {
        FenceWindow? window = _windows.FirstOrDefault(w => w.FenceId == id);
        window?.SetTheme(theme);
    }

    public void SetThemeAll(FenceTheme theme)
    {
        foreach (FenceWindow window in _windows)
            window.SetTheme(theme, persist: false);
        SaveAll();
    }

    public void ResetTheme(Guid id) => SetTheme(id, FenceTheme.Default());

    public void ResetThemeAll() => SetThemeAll(FenceTheme.Default());

    public void SetDefaultAppearanceFromFence(Guid id)
    {
        FenceWindow? window = _windows.FirstOrDefault(w => w.FenceId == id);
        if (window is null)
            return;

        TitleAlignment alignment = window.CurrentTitleAlignment;
        FenceTheme theme = window.CurrentTheme.Normalized();
        _defaultTitleAlignment = alignment == TitleAlignment.Left ? null : alignment;
        _defaultTheme = theme.IsDefault ? null : theme;
        SaveAll();
    }

    public FenceTheme GetTheme(Guid id)
    {
        FenceWindow? window = _windows.FirstOrDefault(w => w.FenceId == id);
        return window?.CurrentTheme ?? FenceTheme.Default();
    }

    public IReadOnlyList<FenceSummary> Summaries() =>
        _windows.Select(w => new FenceSummary(w.FenceId, w.DisplayTitle, w.CurrentTitleAlignment)).ToList();

    internal void SnapAfterMove(FenceWindow window)
    {
        SnapRect next = FenceSnap.Translate(
            window.LayoutSnapRect(),
            window.WorkAreaSnapRect(),
            OtherSnapRects(window));
        window.ApplySnapPosition(next.X, next.Y);
    }

    internal void SnapAfterResize(FenceWindow window)
    {
        if (window.IsRolledUp)
            return;

        SnapRect next = FenceSnap.Edges(
            window.LayoutSnapRect(),
            window.WorkAreaSnapRect(),
            OtherSnapRects(window),
            window.MinWidth,
            window.MinHeight);
        window.ApplySnapRect(next);
    }

    private IReadOnlyList<SnapRect> OtherSnapRects(FenceWindow source)
    {
        var others = new List<SnapRect>(_windows.Count);
        foreach (FenceWindow window in _windows)
        {
            if (ReferenceEquals(window, source) || !window.IsVisible)
                continue;
            others.Add(window.LayoutSnapRect());
        }

        return others;
    }

    internal void NotifyScreenLeftDown()
    {
        _blockDesktopInbound = false;
        _itemDragSource = null;
        ClearDropHighlights();
    }

    internal void CancelFenceItemDrag(FenceWindow source)
    {
        if (!ReferenceEquals(_itemDragSource, source))
            return;
        _itemDragSource = null;
        _blockDesktopInbound = false;
        ClearDropHighlights();
    }

    internal void BeginFenceItemDrag(FenceWindow source)
    {
        _itemDragSource = source;
        _blockDesktopInbound = true;
        ClearDropHighlights();
    }

    internal void UpdateFenceItemDrag(FenceWindow source, int screenX, int screenY)
    {
        if (!ReferenceEquals(_itemDragSource, source))
            return;

        Guid? destId = FenceItemDrop.TransferTargetId(SnapshotScreenTargets(), source.FenceId, screenX, screenY);
        foreach (FenceWindow window in _windows)
            window.ShowDropChrome(destId is { } id && window.FenceId == id);
    }

    internal void CompleteItemDrag(FenceWindow source, IReadOnlyList<FenceItemVm> items, int screenX, int screenY)
    {
        ClearDropHighlights();
        _itemDragSource = null;

        FenceItemDropResult drop = FenceItemDrop.Evaluate(
            SnapshotScreenTargets(), source.FenceId, screenX, screenY);
        if (drop.Kind == FenceItemDropKind.Transfer && drop.TargetId is { } destId)
        {
            FenceWindow? dest = _windows.FirstOrDefault(w => w.FenceId == destId);
            if (dest is not null && TransferItems(source, dest, items, screenX, screenY))
                return;
        }

        if (drop.Kind == FenceItemDropKind.Eject)
        {
            if (!EjectItems(source, items, screenX, screenY))
                source.ShowCustodyError(LastCustodyError);
        }
        else
            source.PersistLayout();
    }

    private bool TransferItems(
        FenceWindow source,
        FenceWindow dest,
        IReadOnlyList<FenceItemVm> items,
        int screenX,
        int screenY)
    {
        List<FenceItemVm> moving = items.Where(item => !dest.ContainsSameItem(item)).ToList();
        if (moving.Count == 0)
            return false;

        LayoutDocument before = CaptureDocument();
        HashSet<Guid> ids = moving.Select(i => i.ItemId).ToHashSet();
        int insert = dest.TransferInsertIndex(screenX, screenY);
        LayoutDocument after;
        try { after = FenceOwnership.Transfer(before, source.FenceId, dest.FenceId, ids, insert); }
        catch { return false; }
        try { _custody.CommitMetadata(before, after, ids); }
        catch { return false; }
        _layoutRevision = after.Revision;
        dest.AcceptTransferredItems(source.DetachForTransfer(moving), screenX, screenY);
        return true;
    }

    private IReadOnlyList<FenceScreenTarget> SnapshotScreenTargets()
    {
        var targets = new List<FenceScreenTarget>(_windows.Count);
        foreach (FenceWindow window in _windows)
        {
            if (window.TryGetScreenTarget(out FenceScreenTarget target))
                targets.Add(target);
        }

        return targets;
    }

    private void ClearDropHighlights()
    {
        foreach (FenceWindow window in _windows)
            window.ShowDropChrome(false);
    }

    private FenceWindow Spawn(FenceState state)
    {
        var window = new FenceWindow(state, this);
        window.LayoutChanged += OnWindowLayoutChanged;
        _windows.Add(window);
        window.Show();
        return window;
    }

    private void OnWindowLayoutChanged() => SaveAll();

    private bool SaveAll()
    {
        if (_saving)
            return false;

        _saving = true;
        try
        {
            LayoutDocument before = CaptureDocument();
            LayoutDocument doc = LayoutStore.Clone(before);
            if (doc.Fences.Count == 0)
                FenceLayoutRules.EnsureAtLeastOne(
                    doc.Fences,
                    Loc.T("DefaultFenceTitle"),
                    _defaultTitleAlignment,
                    _defaultTheme);
            _custody.CommitMetadata(before, doc);
            _layoutRevision = doc.Revision;
            return true;
        }
        catch { return false; }
        finally
        {
            _saving = false;
        }
    }

    internal bool AddItems(FenceWindow target, IReadOnlyList<FenceItemVm> candidates)
    {
        if (_returningItemsToDesktop > 0)
            return false;
        candidates = candidates
            .Where(item => !DesktopFenceStubRules.ForbidsCustody(item.Name, item.Path, item.OriginalPath))
            .ToList();
        var knownNamespace = _windows.SelectMany(window => window.Items)
            .Select(item => item.NamespaceKey).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (candidates.Any(item => item.NamespaceKey is { } key && !knownNamespace.Add(key)))
            return false;
        if (candidates.Count == 0)
            return true;
        IReadOnlyList<DesktopCustodyPlan> plans;
        try { plans = _custody.PlanInbound(candidates.Select(ToCustodyItem)); }
        catch { return false; }
        Dictionary<Guid, DesktopCustodyPlan> byId = plans.ToDictionary(p => p.ItemId);
        foreach (FenceItemVm item in candidates)
        {
            DesktopCustodyPlan plan = byId[item.ItemId];
            item.Kind = plan.Kind;
            item.StorageName = plan.StorageName;
            item.OriginalPath = plan.OriginalPath;
            item.NamespaceKey = plan.NamespaceKey;
        }

        LayoutDocument before = CaptureDocument();
        LayoutDocument after = LayoutStore.Clone(before);
        FenceState? state = after.Fences.FirstOrDefault(f => f.Id == target.FenceId);
        if (state is null)
            return false;
        state.Items.AddRange(candidates.Select(i => i.ToState()));
        if (!_custody.CommitInbound(
                before, after, CustodyOperationKind.Inbound, plans,
                () => target.ApplyInbound(candidates, plans), out _, notifyDesktopDirectory: false))
            return false;
        _layoutRevision = after.Revision;
        return true;
    }

    private bool EjectItems(
        FenceWindow source,
        IReadOnlyList<FenceItemVm> items,
        int? screenX,
        int? screenY)
    {
        LastCustodyError = null;
        if (_returningItemsToDesktop > 0)
            return false;
        IReadOnlyList<DesktopCustodyPlan> plans;
        try { plans = _custody.PlanOutbound(items.Select(ToCustodyItem)); }
        catch (Exception ex) { LastCustodyError = ex.Message; return false; }
        var snapshot = _recoverySnapshots.Load();
        DesktopReturnReservation reservation = _recoveryIcons.ReserveReturnPositions(
            BuildReleasedPlacements(items.Select(item => item.ToState()).ToList(), plans, snapshot, screenX, screenY));
        var accepted = plans.Where(plan => CanReturn(plan, reservation)).ToList();
        if (accepted.Count != plans.Count)
            LastCustodyError = Loc.Format("DesktopReturnDeferred", plans.Count - accepted.Count);
        if (accepted.Count == 0)
            return false;
        LayoutDocument before = CaptureDocument();
        LayoutDocument after = LayoutStore.Clone(before);
        FenceState? state = after.Fences.FirstOrDefault(f => f.Id == source.FenceId);
        if (state is null)
            return false;
        HashSet<Guid> ids = accepted.Select(plan => plan.ItemId).ToHashSet();
        var returned = items.Where(item => ids.Contains(item.ItemId)).ToList();
        state.Items.RemoveAll(i => ids.Contains(i.ItemId));
        if (!_custody.CommitOutbound(
                before, after, CustodyOperationKind.Outbound, accepted,
                () => source.ApplyOutbound(returned, accepted), out string? error, notifyDesktopDirectory: false))
        {
            LastCustodyError = error;
            return false;
        }
        _layoutRevision = after.Revision;
        _ = PlaceAfterEjectAsync(reservation,
            BuildReleasedPlacements(returned.Select(item => item.ToState()).ToList(), accepted, snapshot));
        if (LastCustodyError is not null)
            source.ShowCustodyError(LastCustodyError);
        return true;
    }

    private async Task PlaceAfterEjectAsync(DesktopReturnReservation reservation, IReadOnlyList<DesktopPlacement> placements)
    {
        if (placements.Count == 0)
            return;
        Interlocked.Increment(ref _returningItemsToDesktop);
        try
        {
            var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            const int attempts = 20;
            int consecutive = 0;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                int confirmed = 0;
                try { confirmed = _recoveryIcons.PlaceReservedItems(reservation, placements, handled); }
                catch { /* Os arquivos devolvidos já estão no Desktop. */ }
                consecutive = confirmed >= placements.Count ? consecutive + 1 : 0;
                if (consecutive >= 3)
                    break;
                if (attempt < attempts - 1)
                    await Task.Delay(120).ConfigureAwait(true);
            }
        }
        finally { Interlocked.Decrement(ref _returningItemsToDesktop); }
    }

    internal bool RemoveItems(FenceWindow source, IReadOnlyList<FenceItemVm> items) =>
        EjectItems(source, items, null, null);

    private bool ReleaseAll(CustodyOperationKind operation, bool allowDeferred = false)
    {
        List<FenceItemVm> items = _windows.SelectMany(w => w.Items).ToList();
        if (items.Count == 0)
            return true;
        IReadOnlyList<DesktopCustodyPlan> plans;
        try { plans = _custody.PlanOutbound(items.Select(ToCustodyItem)); }
        catch (Exception ex) { LastCustodyError = ex.Message; return false; }
        var snapshot = _recoverySnapshots.Load();
        var states = items.Select(item => item.ToState()).ToList();
        DesktopReturnReservation reservation =
            _recoveryIcons.ReserveReturnPositions(BuildReleasedPlacements(states, plans, snapshot));
        var accepted = plans.Where(plan => CanReturn(plan, reservation)).ToList();
        if (accepted.Count != plans.Count)
        {
            LastCustodyError = Loc.Format("DesktopReturnDeferred", plans.Count - accepted.Count);
            if (!allowDeferred)
                return false;
        }
        if (accepted.Count == 0)
            return true;
        LayoutDocument before = CaptureDocument();
        LayoutDocument after = LayoutStore.Clone(before);
        if (!_custody.CommitOutbound(
                before, after, operation, accepted,
                () =>
                {
                    foreach (FenceWindow window in _windows)
                        window.ApplyReleasedPaths(after);
                }, out string? error, notifyDesktopDirectory: false))
        {
            LastCustodyError = error;
            return false;
        }
        _layoutRevision = after.Revision;
        var acceptedIds = accepted.Select(plan => plan.ItemId).ToHashSet();
        PlaceReleasedItems(reservation, BuildReleasedPlacements(
            items.Where(item => acceptedIds.Contains(item.ItemId)).Select(item => item.ToState()).ToList(),
            accepted, snapshot));
        return true;
    }

    internal static bool CanReturn(DesktopCustodyPlan plan, DesktopReturnReservation reservation) =>
        !plan.CanPlaceAfterRelease || reservation.HasCell(ReturnPlacementKey(plan));

    private static string ReturnPlacementKey(DesktopCustodyPlan plan) =>
        (plan.Kind == FenceItemKind.Stored ? plan.DestinationPath : plan.NamespaceKey) ?? plan.Name;

    private void PlaceReleasedItems(DesktopReturnReservation reservation, IReadOnlyList<DesktopPlacement> placements)
    {
        var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        RunPlacementRetries(
            () => _recoveryIcons.PlaceReservedItems(reservation, placements, handled),
            placements.Count,
            settlePasses: 3);
    }

    internal static IReadOnlyList<DesktopPlacement> BuildReleasedPlacements(
        IReadOnlyList<FenceItemState> items,
        IReadOnlyList<DesktopCustodyPlan> plans,
        DesktopRecoverySnapshot? snapshot,
        int? screenX = null,
        int? screenY = null)
    {
        Dictionary<Guid, DesktopCustodyPlan> plansById = plans.ToDictionary(plan => plan.ItemId);
        Dictionary<Guid, DesktopRecoveryItem> recoveryById = snapshot?.Items
            .Where(item => item.ItemId.HasValue)
            .GroupBy(item => item.ItemId!.Value)
            .ToDictionary(group => group.Key, group => group.First())
            ?? [];

        var result = new List<DesktopPlacement>(items.Count);
        foreach (FenceItemState item in items)
        {
            plansById.TryGetValue(item.ItemId, out DesktopCustodyPlan? plan);
            if (plan is not null && !plan.CanPlaceAfterRelease)
                continue;
            recoveryById.TryGetValue(item.ItemId, out DesktopRecoveryItem? recovery);
            string nameOrPath = plan is null ? item.OriginalPath ?? item.Name : ReturnPlacementKey(plan);
            result.Add(new DesktopPlacement(
                nameOrPath,
                item.OriginalX ?? recovery?.X,
                item.OriginalY ?? recovery?.Y,
                screenX,
                screenY,
                item.ReleasedIdentity ?? DesktopFileIdentity.Read(plan?.DestinationPath),
                VerifyFileIdentity: item.Kind == FenceItemKind.Stored));
        }

        return result;
    }

    internal static int RunPlacementRetries(
        Func<int> place,
        int expectedCount,
        int maxAttempts = 20,
        Action? wait = null,
        int settlePasses = 1)
    {
        if (expectedCount <= 0 || maxAttempts <= 0)
            return 0;

        int placed = 0;
        int consecutive = 0;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try { placed = place(); }
            catch { placed = 0; }

            consecutive = placed >= expectedCount ? consecutive + 1 : 0;
            if (attempt >= 2 && consecutive >= settlePasses)
                break;
            if (attempt < maxAttempts)
                (wait ?? (() => Thread.Sleep(120)))();
        }

        return placed;
    }

    private bool ConcealAll(CustodyOperationKind operation)
    {
        List<FenceItemVm> items = _windows.SelectMany(w => w.Items).ToList();
        if (items.Count == 0)
            return true;
        IReadOnlyList<DesktopCustodyPlan> plans;
        try { plans = _custody.PlanInbound(items.Select(ToCustodyItem)); }
        catch { return false; }
        LayoutDocument before = CaptureDocument();
        LayoutDocument after = LayoutStore.Clone(before);
        if (!_custody.CommitInbound(
                before, after, operation, plans,
                () =>
                {
                    foreach (FenceWindow window in _windows)
                        window.ApplyStoredPaths(plans);
                }, out _, notifyDesktopDirectory: false))
            return false;
        _layoutRevision = after.Revision;
        return true;
    }

    private LayoutDocument EnsureCustodyBeforeUi(LayoutDocument doc)
    {
        StartupCustodyReconciliation reconciliation = StartupCustodyReconciler.Reconcile(doc);
        if (reconciliation.RemovedItemIds.Count > 0)
        {
            _custody.CommitMetadata(doc, reconciliation.Document, reconciliation.RemovedItemIds);
            doc = reconciliation.Document;
        }

        bool manual = _recoveryIcons.IsManualArrangement();
        var items = new List<DesktopCustodyItem>();
        var pending = new List<string>();
        foreach (FenceItemState item in doc.Fences.SelectMany(fence => fence.Items))
        {
            if (item.Kind == FenceItemKind.Namespace)
            {
                if (item.NamespaceKey is not null && (manual || DesktopCustodyBatch.IsNamespaceAlreadyHidden(item.NamespaceKey)))
                    items.Add(DesktopCustodyItem.FromState(item));
                else
                    pending.Add(item.Name);
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.StorageName))
                continue;
            string storedPath = FenceItemStore.PayloadPath(item.ItemId, item.StorageName);
            bool alreadyStored = File.Exists(storedPath) || Directory.Exists(storedPath);
            if (alreadyStored || (manual && DesktopFileIdentity.Matches(item.ReleasedPath, item.ReleasedIdentity)))
                items.Add(DesktopCustodyItem.FromState(item));
            else
                pending.Add(item.Name);
        }
        StartupWarnings = pending;
        if (items.Count == 0)
            return doc;
        IReadOnlyList<DesktopCustodyPlan> plans = _custody.PlanInbound(items);
        LayoutDocument after = LayoutStore.Clone(doc);
        if (!_custody.CommitInbound(
                doc, after, CustodyOperationKind.Resume, plans, null, out string? error, notifyDesktopDirectory: false))
            throw new IOException(error ?? "Falha ao recuperar a custódia do Desktop.");
        return after;
    }

    private LayoutDocument CaptureDocument() => new()
    {
        Version = LayoutDocument.CurrentVersion,
        Revision = _layoutRevision,
        UiLanguage = UiLanguage,
        DefaultTitleAlignment = _defaultTitleAlignment,
        DefaultTheme = _defaultTheme,
        Fences = _windows.Select(w => w.CaptureState()).ToList()
    };

    private static DesktopCustodyItem ToCustodyItem(FenceItemVm item) => new(
        item.ItemId, item.Kind, item.Name, item.Path,
        item.OriginalPath, item.StorageName, item.NamespaceKey, item.ReleasedPath, item.ReleasedIdentity);
}

public sealed record FenceSummary(Guid Id, string Title, TitleAlignment TitleAlignment);
