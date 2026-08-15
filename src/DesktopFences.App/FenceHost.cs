using DesktopFences.App.Localization;
using DesktopFences.App.ViewModels;
using DesktopFences.Core.Fences;
using DesktopFences.Core.Models;
using DesktopFences.Core.Occupancy;
using DesktopFences.Core.Persistence;

namespace DesktopFences.App;

public sealed class FenceHost
{
    private readonly LayoutStore _store = new();
    private readonly List<FenceWindow> _windows = [];
    private bool _saving;
    private bool _paused;
    private FenceWindow? _itemDragSource;
    private bool _blockDesktopInbound;

    public event Action? FencesChanged;

    public bool IsPaused => _paused;

    public string UiLanguage { get; private set; } = UiLanguageCodes.System;

    public IReadOnlyList<FenceWindow> Windows => _windows;

    public bool IsBlockingDesktopInbound => _blockDesktopInbound;

    public void Start()
    {
        LayoutDocument doc = _store.LoadOrEmpty();
        UiLanguage = UiLanguageCodes.Normalize(doc.UiLanguage);
        UiLocale.Apply(UiLanguage);
        FenceLayoutRules.EnsureAtLeastOne(doc.Fences, Loc.T("DefaultFenceTitle"));
        foreach (FenceState state in doc.Fences)
            Spawn(state);

        SaveAll();
        FencesChanged?.Invoke();
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

    public void PauseAll()
    {
        _paused = true;
        foreach (FenceWindow window in _windows)
            window.Pause();
    }

    public void ResumeAll()
    {
        _paused = false;
        foreach (FenceWindow window in _windows)
            window.Resume();
    }

    public void RestoreAllIcons()
    {
        foreach (FenceWindow window in _windows)
            window.RestoreHiddenIcons();
    }

    public void PrepareExit()
    {
        SaveAll();
        foreach (FenceWindow window in _windows.ToList())
        {
            window.LayoutChanged -= OnWindowLayoutChanged;
            window.SuppressPersistOnClose = true;
            window.RestoreHiddenIcons();
            window.Close();
        }

        _windows.Clear();
    }

    public bool TryAddNew()
    {
        List<FenceState> current = _windows.Select(w => w.CaptureState()).ToList();
        FenceState state = FenceLayoutRules.PlaceNew(current, Loc.T("DefaultFenceTitle"));
        FenceWindow window = Spawn(state);
        if (_paused)
            window.Pause();
        SaveAll();
        FencesChanged?.Invoke();
        return true;
    }

    public bool TryRemove(Guid id)
    {
        if (!FenceLayoutRules.CanRemove(_windows.Count))
            return false;

        FenceWindow? window = _windows.FirstOrDefault(w => w.FenceId == id);
        if (window is null)
            return false;

        window.LayoutChanged -= OnWindowLayoutChanged;
        window.RestoreHiddenIcons();
        window.SuppressPersistOnClose = true;
        _windows.Remove(window);
        if (System.Windows.Application.Current.MainWindow == window || System.Windows.Application.Current.MainWindow is null)
            System.Windows.Application.Current.MainWindow = _windows.FirstOrDefault();
        window.Close();
        SaveAll();
        FencesChanged?.Invoke();
        return true;
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

    public FenceTheme GetTheme(Guid id)
    {
        FenceWindow? window = _windows.FirstOrDefault(w => w.FenceId == id);
        return window?.CurrentTheme ?? FenceTheme.Default();
    }

    public IReadOnlyList<FenceSummary> Summaries() =>
        _windows.Select(w => new FenceSummary(w.FenceId, w.DisplayTitle, w.CurrentTitleAlignment)).ToList();

    internal void NotifyScreenLeftDown()
    {
        _blockDesktopInbound = false;
        _itemDragSource = null;
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
            {
                SaveAll();
                return;
            }
        }

        if (drop.Kind == FenceItemDropKind.Eject)
            source.EjectItemsToDesktop(items, screenX, screenY);

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

        IReadOnlyList<DesktopIcon> tracked = source.ReleaseTracked(moving);
        dest.AcceptTransferredItems(source.DetachForTransfer(moving), tracked, screenX, screenY);
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

    private void SaveAll()
    {
        if (_saving)
            return;

        _saving = true;
        try
        {
            var doc = new LayoutDocument
            {
                UiLanguage = UiLanguage,
                Fences = _windows.Select(w => w.CaptureState()).ToList()
            };
            if (doc.Fences.Count == 0)
                FenceLayoutRules.EnsureAtLeastOne(doc.Fences, Loc.T("DefaultFenceTitle"));
            _store.Save(doc);
        }
        catch { }
        finally
        {
            _saving = false;
        }
    }
}

public sealed record FenceSummary(Guid Id, string Title, TitleAlignment TitleAlignment);
