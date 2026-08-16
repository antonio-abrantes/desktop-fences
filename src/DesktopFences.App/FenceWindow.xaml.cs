using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DesktopFences.App.Localization;
using DesktopFences.App.Services;
using DesktopFences.App.ViewModels;
using DesktopFences.Core;
using DesktopFences.Core.Fences;
using DesktopFences.Core.Models;
using DesktopFences.Core.Occupancy;
using DesktopFences.Native;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Color = System.Windows.Media.Color;

namespace DesktopFences.App;

public partial class FenceWindow : Window
{
    private const double TitleBarHeight = 36;
    private const double ChromeCornerRadius = 8;
    private const double DefaultExpandedHeight = 280;
    private const double TileWidth = 88;
    private const double TileHeight = 84;

    private readonly DesktopIconService _desktop = new();
    private readonly ObservableCollection<FenceItemVm> _items = [];
    private readonly Guid _fenceId;
    private readonly FenceState? _bootState;
    private readonly FenceHost? _host;

    private bool _collapsed;
    private double _expandedHeight = DefaultExpandedHeight;
    private TitleAlignment _titleAlignment = TitleAlignment.Left;
    private FenceItemVm? _selected;
    private MouseButtonWatch? _mouseWatch;
    private ShellOleDropTarget? _oleDrop;
    private InvisibleFileDropLayer? _oleHit;
    private bool _pressOnFence;
    private DesktopIcon? _pressIcon;
    private List<DesktopIcon>? _pressIcons;
    private bool _dropConsumed;
    private bool _inboundOle;
    private int _downX;
    private int _downY;
    private System.Windows.Point _tileDragStart;
    private bool _tileDragArmed;
    private List<FenceItemVm>? _draggingItems;
    private FenceItemVm? _pendingSingleSelect;
    private DragGhostWindow? _ghost;
    private DispatcherTimer? _cursorPump;
    private HwndSource? _hwndSource;
    private bool _renaming;
    private bool _dropBorderLit;
    private FenceTheme _theme = FenceTheme.Default();
    private bool _stayOnDesktop = true;

    public event Action? LayoutChanged;

    public Guid FenceId => _fenceId;

    public bool SuppressPersistOnClose { get; set; }

    public FenceWindow() : this(null)
    {
    }

    public FenceWindow(FenceState? state, FenceHost? host = null)
    {
        InitializeComponent();
        _bootState = state;
        _host = host;
        _fenceId = state?.Id ?? Guid.NewGuid();
        _titleAlignment = state?.TitleAlignment ?? TitleAlignment.Left;
        _theme = (state?.Theme ?? FenceTheme.Default()).Normalized();
        IconGrid.ItemsSource = _items;
        _items.CollectionChanged += (_, _) => UpdateEmptyHint();
        UiLocale.Changed += OnUiLanguageChanged;
        ApplyChromeStrings();
        StateChanged += OnStateChanged;
        DpiChanged += OnDpiChanged;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    public void Pause()
    {
        _stayOnDesktop = false;
        RestoreHiddenIcons();
        Hide();
        EndInboundCursor();
        _oleHit?.Withdraw();
        _ghost?.HideGhost();
    }

    public void Resume()
    {
        Show();
        _stayOnDesktop = true;
        HideDesktopCounterparts();
        SendBehindApps();
    }

    public bool RestoreHiddenIcons()
    {
        try
        {
            bool all = _desktop.RevealAllHidden();
            _desktop.FlushShell();
            return all;
        }
        catch
        {
            return false;
        }
    }

    public void RebindAfterExplorer()
    {
        HideDesktopCounterparts();
        EnsureDesktopSurvival();
    }

    public void EnsureDesktopSurvival()
    {
        if (!_stayOnDesktop)
            return;

        bool restored = false;
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
            restored = true;
        }

        if (!IsVisible)
        {
            Show();
            restored = true;
        }

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        DesktopWindowAnchor.KeepOnDesktop(hwnd);
        if (restored)
            SendBehindApps();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (!_stayOnDesktop || WindowState != WindowState.Minimized)
            return;

        WindowState = WindowState.Normal;
        SendBehindApps();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!_stayOnDesktop || e.NewValue is not false)
            return;

        Dispatcher.BeginInvoke(() =>
        {
            if (_stayOnDesktop)
                EnsureDesktopSurvival();
        }, DispatcherPriority.Input);
    }

    private void OnDpiChanged(object sender, System.Windows.DpiChangedEventArgs e)
    {
        UpdateRoundedClip();
        SendBehindApps();
    }

    private void OnSourceInitialized(object sender, EventArgs e)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        DesktopWindowAnchor.HideFromTaskSwitchers(hwnd);
        DesktopWindowAnchor.PreventMinimizeMaximize(hwnd);
        DesktopWindowAnchor.ExcludeFromPeek(hwnd);
        DesktopWindowAnchor.SendBehindApps(hwnd);
        DesktopWindowAnchor.RegisterDesktopHook(hwnd);
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(OnWndProc);
    }

    private IntPtr OnWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        IntPtr result = DesktopWindowAnchor.FilterDesktopSurvival(
            hwnd, msg, wParam, lParam, _stayOnDesktop, out bool block);
        if (block)
            handled = true;
        return result;
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_renaming && !TitleEdit.IsKeyboardFocusWithin)
            EndRename();
        SendBehindApps();
    }

    private void SendBehindApps()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        DesktopWindowAnchor.SendBehindApps(hwnd);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RestoreLayout();
        ApplyChromeStrings();
        ShowResizeHandles(!_collapsed);
        HideDesktopCounterparts();
        UpdateEmptyHint();
        AttachDesktopDropIntake();
        SizeChanged += OnFenceSizeChanged;
        UpdateRoundedClip();
    }

    private void OnFenceSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateEastResizeForScroll();
        UpdateRoundedClip();
    }

    private void UpdateRoundedClip()
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width < 1 || height < 1)
            return;

        // Só a Window: clip no Chrome comia o traço da borda.
        Clip = new RectangleGeometry(new Rect(0, 0, width, height), ChromeCornerRadius, ChromeCornerRadius);
    }

    private void OnClosed(object sender, EventArgs e)
    {
        _stayOnDesktop = false;
        StateChanged -= OnStateChanged;
        DpiChanged -= OnDpiChanged;
        IsVisibleChanged -= OnIsVisibleChanged;
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        DesktopWindowAnchor.UnregisterDesktopHook(hwnd);
        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(OnWndProc);
            _hwndSource = null;
        }

        UiLocale.Changed -= OnUiLanguageChanged;
        DetachDesktopDropIntake();
        if (!SuppressPersistOnClose)
            SaveLayout();
        RestoreHiddenIcons();
    }

    private void AttachDesktopDropIntake()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        AllowDrop = false;
        _oleDrop = new ShellOleDropTarget();
        _oleDrop.FilesDropped += OnOverlayFilesDropped;
        _oleDrop.DragEntered += OnOleDragEntered;
        _oleDrop.DragLeft += OnOleDragLeft;
        _oleDrop.Attach(hwnd);

        _oleHit = new InvisibleFileDropLayer();
        _oleHit.Prepare();
        _oleDrop.Register(_oleHit.Handle);

        _mouseWatch = new MouseButtonWatch();
        _mouseWatch.LeftDown += OnScreenLeftDown;
        _mouseWatch.LeftMove += OnScreenLeftMove;
        _mouseWatch.LeftUp += OnScreenLeftUp;
    }

    private void DetachDesktopDropIntake()
    {
        if (_mouseWatch is not null)
        {
            _mouseWatch.LeftDown -= OnScreenLeftDown;
            _mouseWatch.LeftMove -= OnScreenLeftMove;
            _mouseWatch.LeftUp -= OnScreenLeftUp;
            _mouseWatch.Dispose();
            _mouseWatch = null;
        }

        _oleHit?.Withdraw();
        _oleHit?.Dispose();
        _oleHit = null;

        EndInboundCursor();

        if (_oleDrop is not null)
        {
            _oleDrop.FilesDropped -= OnOverlayFilesDropped;
            _oleDrop.DragEntered -= OnOleDragEntered;
            _oleDrop.DragLeft -= OnOleDragLeft;
            _oleDrop.Dispose();
            _oleDrop = null;
        }

        _ghost?.HideGhost();
        _ghost?.Close();
        _ghost = null;
    }

    private void OnScreenLeftDown(int x, int y)
    {
        _host?.NotifyScreenLeftDown();
        _dropConsumed = false;
        _pressOnFence = ContainsScreenPoint(x, y);
        _downX = x;
        _downY = y;
        _pressIcons = null;
        if (_renaming && !_pressOnFence)
            EndRename();
        if (_pressOnFence)
        {
            _pressIcon = null;
            return;
        }

        _pressIcon = _desktop.HitTestScreen(x, y);
    }

    private void OnScreenLeftMove(int x, int y)
    {
        int dx = x - _downX;
        int dy = y - _downY;
        int threshold = DragThresholdPx();
        bool moved = (dx * dx) + (dy * dy) >= threshold * threshold;

        if (_draggingItems is { Count: > 0 })
        {
            UpdateItemDrag(x, y);
            return;
        }

        if (_pressOnFence && _tileDragArmed && moved)
        {
            List<FenceItemVm> selected = SelectedItems();
            if (selected.Count > 0)
                BeginItemDrag(selected);
            UpdateItemDrag(x, y);
            return;
        }

        UpdateInboundDesktopDrag(x, y, moved);
    }

    private void UpdateInboundDesktopDrag(int x, int y, bool moved)
    {
        if (ShouldIgnoreDesktopInbound())
            return;
        if (_pressOnFence || _collapsed || _oleHit is null)
            return;
        if (!moved && _pressIcon is null)
            return;

        if (!ContainsScreenPoint(x, y))
        {
            _oleHit.Withdraw();
            if (_inboundOle)
            {
                _inboundOle = false;
                _ghost?.HideGhost();
                SetChromeBorder(drop: false);
                EndInboundCursor();
            }

            return;
        }

        _oleHit.FollowCursor(x, y);
        EnsureInboundPressSet();
        ShowInboundGhost();
        DragCursorOverride.Pulse();
        _ghost?.FollowScreen(x, y, this);
        if (_ghost is not null)
        {
            IntPtr ghostHwnd = new WindowInteropHelper(_ghost).Handle;
            InvisibleOleWindow.RaiseTopMost(ghostHwnd);
        }
    }

    private void EnsureInboundPressSet()
    {
        if (_pressIcons is not null)
            return;

        var selected = new List<DesktopIcon>();
        if (_pressIcon is not null)
        {
            selected.AddRange(_desktop.GetSelectedIcons());
            if (selected.TrueForAll(i => i.Index != _pressIcon.Index))
                selected.Insert(0, _pressIcon);
        }

        _pressIcons = selected;
    }

    private void ShowInboundGhost()
    {
        if (_draggingItems is { Count: > 0 })
            return;

        EnsureInboundPressSet();
        DesktopIcon? lead = _pressIcons is { Count: > 0 } ? _pressIcons[0] : _pressIcon;
        string name = lead?.Name ?? "Arquivo";
        ImageSource? icon = IconImageLoader.Load(name);
        int extra = Math.Max(0, (_pressIcons?.Count ?? 0) - 1);
        _ghost ??= new DragGhostWindow();
        _ghost.ShowItems(icon, DesktopPaths.VisibleName(name), extra);
        _inboundOle = true;
        SetChromeBorder(drop: true);
        BeginInboundCursor();
    }

    private void BeginInboundCursor()
    {
        DragCursorOverride.Begin();
        _cursorPump ??= new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(1)
        };
        _cursorPump.Tick -= CursorPump_Tick;
        _cursorPump.Tick += CursorPump_Tick;
        if (!_cursorPump.IsEnabled)
            _cursorPump.Start();
    }

    private void CursorPump_Tick(object? sender, EventArgs e) => DragCursorOverride.Pulse();

    private void EndInboundCursor()
    {
        if (_cursorPump is not null)
        {
            _cursorPump.Stop();
            _cursorPump.Tick -= CursorPump_Tick;
        }

        DragCursorOverride.End();
    }

    private void OnOleDragEntered()
    {
        if (_draggingItems is { Count: > 0 } || ShouldIgnoreDesktopInbound())
            return;
        ShowInboundGhost();
    }

    private void OnOleDragLeft()
    {
        // O hit window some ao sair da fence; o hook já esconde o ghost.
    }

    private bool ShouldIgnoreDesktopInbound() =>
        _host is { IsBlockingDesktopInbound: true } && _draggingItems is null;

    private int DragThresholdPx()
    {
        PresentationSource? source = PresentationSource.FromVisual(this);
        double scale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1;
        return Math.Max(6, (int)Math.Round(6 * scale));
    }


    private void OnScreenLeftUp(int x, int y)
    {
        SetChromeBorder(drop: false);

        if (_draggingItems is { Count: > 0 })
        {
            EndItemDrag(x, y);
            _pressIcon = null;
            _pressOnFence = false;
            _dropConsumed = false;
            _tileDragArmed = false;
            _inboundOle = false;
            _pressIcons = null;
            EndInboundCursor();
            return;
        }

        if (_inboundOle)
        {
            _inboundOle = false;
            _ghost?.HideGhost();
        }

        EndInboundCursor();
        _oleHit?.Withdraw();

        if (!_dropConsumed && !_pressOnFence && ContainsScreenPoint(x, y) && !ShouldIgnoreDesktopInbound())
            AddInboundDesktopIcons();

        _pressIcon = null;
        _pressIcons = null;
        _pressOnFence = false;
        _dropConsumed = false;
        _tileDragArmed = false;
        _inboundOle = false;
        ApplyPendingSingleSelect();
    }

    private void OnOverlayFilesDropped(IReadOnlyList<string> files)
    {
        _dropConsumed = true;
        if (ShouldIgnoreDesktopInbound())
            return;

        if (files.Count > 0)
        {
            foreach (string file in files)
                AddDesktopEntry(file, null, null);
        }

        AddInboundDesktopIcons();
        HideDesktopCounterparts();
        SaveLayout();
        SetChromeBorder(drop: false);
    }

    private void AddInboundDesktopIcons()
    {
        EnsureInboundPressSet();
        if (_pressIcons is { Count: > 0 })
        {
            foreach (DesktopIcon icon in _pressIcons)
                AddDesktopEntry(icon.Name, icon.X, icon.Y);
            return;
        }

        if (_pressIcon is not null)
            AddDesktopEntry(_pressIcon.Name, _pressIcon.X, _pressIcon.Y);
    }

    private System.Drawing.Rectangle FenceBodyScreenRect()
    {
        double top = TitleBar.IsVisible ? TitleBar.ActualHeight : 0;
        System.Windows.Point topLeft = PointToScreen(new System.Windows.Point(0, top));
        System.Windows.Point bottomRight = PointToScreen(new System.Windows.Point(ActualWidth, ActualHeight));
        int width = Math.Max(1, (int)Math.Round(bottomRight.X - topLeft.X));
        int height = Math.Max(1, (int)Math.Round(bottomRight.Y - topLeft.Y));
        return new System.Drawing.Rectangle((int)Math.Round(topLeft.X), (int)Math.Round(topLeft.Y), width, height);
    }

    private bool ContainsScreenPoint(int x, int y)
    {
        if (!IsVisible || WindowState == WindowState.Minimized)
            return false;
        return FenceScreenRect().Contains(x, y);
    }

    private System.Drawing.Rectangle FenceScreenRect()
    {
        System.Windows.Point topLeft = PointToScreen(new System.Windows.Point(0, 0));
        System.Windows.Point bottomRight = PointToScreen(new System.Windows.Point(ActualWidth, ActualHeight));
        int width = Math.Max(1, (int)Math.Round(bottomRight.X - topLeft.X));
        int height = Math.Max(1, (int)Math.Round(bottomRight.Y - topLeft.Y));
        return new System.Drawing.Rectangle((int)Math.Round(topLeft.X), (int)Math.Round(topLeft.Y), width, height);
    }

    private void MoveGrip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        e.Handled = true;
        DragMove();
        _host?.SnapAfterMove(this);
        SaveLayout();
    }

    private void TitleDisplay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            BeginRename();
            e.Handled = true;
        }
    }

    private void TitleBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ClickCount < 2)
            return;

        if (e.OriginalSource is DependencyObject source
            && (IsInside(source, TitleDisplay) || IsInside(source, TitleEdit)
                || IsInside(source, MoveGrip) || IsInside(source, BtnCollapse)))
            return;

        ToggleCollapse();
        e.Handled = true;
    }

    private static bool IsInside(DependencyObject source, DependencyObject target)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (ReferenceEquals(current, target))
                return true;
            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_renaming || e.ChangedButton != MouseButton.Left)
            return;

        if (e.OriginalSource is DependencyObject source && IsInside(source, TitleEdit))
            return;

        EndRename();
    }

    private void BeginRename()
    {
        if (_renaming)
            return;

        _renaming = true;
        TitleEdit.Text = TitleDisplay.Text;
        TitleDisplay.Visibility = Visibility.Collapsed;
        TitleEdit.Visibility = Visibility.Visible;
        TitleEdit.Focus();
        TitleEdit.SelectAll();
    }

    private void EndRename()
    {
        if (!_renaming)
            return;

        _renaming = false;
        TitleDisplay.Text = CommittedTitle(TitleEdit.Text);
        TitleEdit.Visibility = Visibility.Collapsed;
        TitleDisplay.Visibility = Visibility.Visible;
        SaveLayout();
    }

    private void CancelRename()
    {
        if (!_renaming)
            return;

        TitleEdit.Text = TitleDisplay.Text;
        EndRename();
    }

    private static string CommittedTitle(string? text)
    {
        string title = (text ?? string.Empty).Trim();
        return title.Length == 0 ? Loc.T("DefaultFenceTitle") : title;
    }

    private void ToggleCollapse_Click(object sender, RoutedEventArgs e) => ToggleCollapse();

    private void ToggleCollapse_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        e.Handled = true;
        ToggleCollapse();
    }

    private void BtnDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var diagnostics = new DiagnosticsWindow(_desktop);
        diagnostics.Owner = this;
        diagnostics.Show();
    }

    private void TitleEdit_LostFocus(object sender, RoutedEventArgs e) => EndRename();

    private void TitleEdit_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelRename();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            EndRename();
            e.Handled = true;
        }
    }

    private void OnPreviewDragOver(object sender, DragEventArgs e) => HandleDragOver(e);

    private void OnDragOver(object sender, DragEventArgs e) => HandleDragOver(e);

    private void HandleDragOver(DragEventArgs e)
    {
        e.Effects = ShellFileDrop.AcceptWhileDragging(e);
        e.Handled = true;
        SetChromeBorder(drop: true);
    }

    private void OnDragEnter(object sender, DragEventArgs e) => HandleDragOver(e);

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        SetChromeBorder(drop: false);
    }

    private void OnPreviewDrop(object sender, DragEventArgs e) => HandleDrop(e);

    private void OnDrop(object sender, DragEventArgs e) => HandleDrop(e);

    private void HandleDrop(DragEventArgs e)
    {
        SetChromeBorder(drop: false);
        IReadOnlyList<string> files = ShellFileDrop.ExtractPaths(e.Data);
        if (files.Count == 0)
            return;

        foreach (string file in files)
            AddDesktopEntry(file, null, null);

        HideDesktopCounterparts();
        SaveLayout();
        e.Handled = true;
    }

    private void Body_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: FenceItemVm })
            return;

        _pendingSingleSelect = null;
        ClearSelection();
    }

    private void IconTile_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FenceItemVm item })
            return;

        if (e.ChangedButton != MouseButton.Left)
        {
            _selected = item;
            return;
        }

        if (e.ClickCount >= 2)
        {
            OpenItem(item);
            e.Handled = true;
            return;
        }

        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (ctrl)
        {
            item.IsSelected = !item.IsSelected;
            _pendingSingleSelect = null;
        }
        else if (!item.IsSelected)
        {
            ClearSelection();
            item.IsSelected = true;
            _pendingSingleSelect = null;
        }
        else
            _pendingSingleSelect = item;

        _selected = item;
        _tileDragStart = e.GetPosition(this);
        _tileDragArmed = true;
    }

    private void IconTile_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_tileDragArmed || e.LeftButton != MouseButtonState.Pressed)
            return;
        if (sender is not FrameworkElement { DataContext: FenceItemVm item })
            return;

        System.Windows.Point now = e.GetPosition(this);
        if (Math.Abs(now.X - _tileDragStart.X) < 6 && Math.Abs(now.Y - _tileDragStart.Y) < 6)
            return;

        List<FenceItemVm> selected = SelectedItems();
        if (!selected.Contains(item))
        {
            ClearSelection();
            item.IsSelected = true;
            selected = [item];
        }

        BeginItemDrag(selected);
        System.Windows.Point screen = PointToScreen(now);
        UpdateItemDrag((int)Math.Round(screen.X), (int)Math.Round(screen.Y));
        e.Handled = true;
    }

    private void IconTile_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _tileDragArmed = false;
        if (_draggingItems is null)
            ApplyPendingSingleSelect();
    }

    private void BeginItemDrag(List<FenceItemVm> selected)
    {
        if (selected.Count == 0)
            return;
        if (_draggingItems is not null)
            return;

        _draggingItems = selected;
        _pendingSingleSelect = null;
        _tileDragArmed = false;
        foreach (FenceItemVm item in _items)
            item.IsDragging = selected.Contains(item);

        FenceItemVm lead = selected[0];
        _ghost ??= new DragGhostWindow();
        _ghost.ShowItems(lead.Icon, lead.DisplayName, selected.Count - 1);
        _host?.BeginFenceItemDrag(this);
    }

    private void UpdateItemDrag(int screenX, int screenY)
    {
        if (_draggingItems is null)
            return;

        _ghost?.FollowScreen(screenX, screenY, this);
        _host?.UpdateFenceItemDrag(this, screenX, screenY);
        if (ContainsBodyScreenPoint(screenX, screenY))
            ReorderDraggingTo(screenX, screenY);
    }

    private void EndItemDrag(int screenX, int screenY)
    {
        _ghost?.HideGhost();
        foreach (FenceItemVm item in _items)
            item.IsDragging = false;

        List<FenceItemVm> dragged = _draggingItems ?? [];
        _draggingItems = null;
        _pendingSingleSelect = null;

        if (dragged.Count == 0)
            return;

        if (_host is not null)
        {
            _host.CompleteItemDrag(this, dragged, screenX, screenY);
            return;
        }

        if (!ContainsScreenPoint(screenX, screenY))
        {
            foreach (FenceItemVm eject in dragged)
                EjectToDesktop(eject, screenX, screenY);
        }

        SaveLayout();
    }

    private void ReorderDraggingTo(int screenX, int screenY)
    {
        if (_draggingItems is null || _draggingItems.Count == 0)
            return;

        System.Windows.Point local = IconGrid.PointFromScreen(new System.Windows.Point(screenX, screenY));
        double panelWidth = IconGrid.ActualWidth > 1 ? IconGrid.ActualWidth : BodyHost.ActualWidth;
        int insert = IconGridReorder.InsertIndex(
            _items.Count, local.X, local.Y, TileWidth, TileHeight, panelWidth);

        if (_draggingItems.Count == 1)
        {
            int at = _items.IndexOf(_draggingItems[0]);
            if (at >= 0 && (insert == at || insert == at + 1))
                return;
        }

        IconGridReorder.MoveBlock(_items, _draggingItems, insert);
    }

    private bool ContainsBodyScreenPoint(int x, int y) =>
        !_collapsed && FenceBodyScreenRect().Contains(x, y);

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        FenceItemVm? item = _selected;
        if (sender is MenuItem menu)
        {
            if (menu.DataContext is FenceItemVm fromItem)
                item = fromItem;
            else if (menu.Parent is ContextMenu { PlacementTarget: FrameworkElement target })
                item = target.DataContext as FenceItemVm ?? item;
        }

        if (item is null)
            return;

        if (!RestoreDesktopItem(item))
            return;

        _items.Remove(item);
        SaveLayout();
        UpdateEmptyHint();
    }

    private void ToggleCollapse()
    {
        if (_collapsed)
            Expand();
        else
            Collapse();
    }

    private void Collapse()
    {
        Collapse(persist: true, captureExpandedSize: true);
    }

    private void Collapse(bool persist, bool captureExpandedSize)
    {
        if (_collapsed)
            return;

        EndRename();
        // No restore o ActualHeight ainda é o default do XAML — não pode substituir a altura gravada.
        if (captureExpandedSize && ActualHeight > TitleBarHeight + 8)
            _expandedHeight = Math.Max(TitleBarHeight + 80, ActualHeight);
        _collapsed = true;
        BtnCollapseGlyph.Text = "▾";
        BodyHost.Visibility = Visibility.Collapsed;
        BeginAnimation(HeightProperty, null);
        MinHeight = TitleBarHeight;
        MaxHeight = TitleBarHeight;
        Height = TitleBarHeight;
        ShowResizeHandles(false);
        ApplyChromeStrings();
        if (persist)
            SaveLayout();
    }

    private void Expand()
    {
        if (!_collapsed)
            return;

        _collapsed = false;
        BtnCollapseGlyph.Text = "▴";
        BodyHost.Visibility = Visibility.Visible;
        MaxHeight = double.PositiveInfinity;
        MinHeight = 80;
        ShowResizeHandles(true);
        ApplyChromeStrings();
        AnimateHeight(_expandedHeight);
        SaveLayout();
    }

    private void OnBodyScrollChanged(object sender, ScrollChangedEventArgs e)
        => UpdateEastResizeForScroll();

    private void UpdateEastResizeForScroll()
    {
        if (_collapsed)
        {
            ResizeE.Visibility = Visibility.Collapsed;
            return;
        }

        bool scrollbarVisible = BodyHost.ComputedVerticalScrollBarVisibility == Visibility.Visible;
        ResizeE.Visibility = scrollbarVisible ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowResizeHandles(bool visible)
    {
        Visibility state = visible ? Visibility.Visible : Visibility.Collapsed;
        ResizeN.Visibility = state;
        ResizeS.Visibility = state;
        ResizeW.Visibility = state;
        ResizeNW.Visibility = state;
        ResizeNE.Visibility = state;
        ResizeSW.Visibility = state;
        ResizeSE.Visibility = state;
        if (visible)
            UpdateEastResizeForScroll();
        else
            ResizeE.Visibility = Visibility.Collapsed;
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_collapsed || sender is not Thumb { Tag: string edge })
            return;

        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);

        double width = Width;
        double height = ActualHeight > 0 ? ActualHeight : Height;
        double minW = MinWidth;
        double minH = MinHeight;

        if (edge.Contains('E'))
            width = Math.Max(minW, width + e.HorizontalChange);
        if (edge.Contains('S'))
            height = Math.Max(minH, height + e.VerticalChange);
        if (edge.Contains('W'))
        {
            double next = Math.Max(minW, width - e.HorizontalChange);
            Left += width - next;
            width = next;
        }
        if (edge.Contains('N'))
        {
            double next = Math.Max(minH, height - e.VerticalChange);
            Top += height - next;
            height = next;
        }

        Width = width;
        Height = height;
    }

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!_collapsed)
            _expandedHeight = ActualHeight > 0 ? ActualHeight : Height;
        _host?.SnapAfterResize(this);
        SaveLayout();
    }

    private void AnimateHeight(double to)
    {
        BeginAnimation(HeightProperty, null);
        var animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        animation.Completed += (_, _) => BeginAnimation(HeightProperty, null);
        BeginAnimation(HeightProperty, animation);
    }

    private void AddFile(string path) => AddDesktopEntry(path, null, null);

    private void AddDesktopEntry(string nameOrPath, int? originalX, int? originalY)
    {
        string? resolved = DesktopPaths.ResolveExisting(nameOrPath);
        DesktopSnapshot snap = _desktop.Capture();
        DesktopIcon? desktopIcon = snap.Connected
            ? DesktopIconMatcher.Find(snap.Icons, resolved ?? nameOrPath)
              ?? DesktopIconMatcher.Find(snap.Icons, DesktopPaths.VisibleName(resolved ?? nameOrPath))
            : null;

        string name = string.IsNullOrWhiteSpace(desktopIcon?.Name)
            ? DesktopPaths.VisibleName(resolved ?? nameOrPath)
            : desktopIcon.Name;

        if (_items.Any(i => string.Equals(i.Path, resolved, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(i.Name, nameOrPath, StringComparison.OrdinalIgnoreCase)))
            return;

        _items.Add(new FenceItemVm
        {
            Name = name,
            Path = resolved ?? desktopIcon?.Name ?? nameOrPath,
            OriginalPath = resolved is not null && !FenceItemStore.IsUnderRoot(resolved)
                ? resolved
                : null,
            Icon = IconImageLoader.Load(resolved ?? desktopIcon?.Name ?? nameOrPath),
            OriginalX = originalX ?? desktopIcon?.X,
            OriginalY = originalY ?? desktopIcon?.Y
        });

        HideDesktopCounterparts();
        SaveLayout();
        UpdateEmptyHint();
    }

    private void EjectToDesktop(FenceItemVm item, int screenX, int screenY)
    {
        if (!RestoreDesktopItem(item, screenX, screenY))
            return;

        _items.Remove(item);
        UpdateEmptyHint();
    }

    internal void EjectItemsToDesktop(IReadOnlyList<FenceItemVm> items, int screenX, int screenY)
    {
        var leaving = new List<FenceItemVm>();
        foreach (FenceItemVm item in items)
        {
            if (!_desktop.TryRevealDesktopItem(item.Path, item.Name, item.OriginalPath, out string? restored))
                continue;
            if (!string.IsNullOrEmpty(restored))
                item.Path = restored;
            _items.Remove(item);
            leaving.Add(item);
        }

        _desktop.FlushShell();
        foreach (FenceItemVm item in leaving)
            TryPlaceRestoredIcon(item, screenX, screenY);

        UpdateEmptyHint();
    }

    internal bool ContainsSameItem(FenceItemVm item) =>
        _items.Any(existing => SameItem(existing, item));

    internal void DetachHidden(IEnumerable<FenceItemVm> items)
    {
        foreach (FenceItemVm item in items)
            _desktop.ForgetHidden(item.Path, item.Name, item.OriginalPath);
    }

    internal List<FenceItemVm> DetachForTransfer(IReadOnlyList<FenceItemVm> items)
    {
        var moved = new List<FenceItemVm>(items.Count);
        foreach (FenceItemVm item in items)
        {
            if (!_items.Remove(item))
                continue;
            item.IsDragging = false;
            item.IsSelected = false;
            moved.Add(item);
        }

        if (_selected is not null && !_items.Contains(_selected))
            _selected = null;
        UpdateEmptyHint();
        return moved;
    }

    internal void AcceptTransferredItems(
        IReadOnlyList<FenceItemVm> items,
        int screenX,
        int screenY)
    {
        int insert = InsertIndexAtScreen(screenX, screenY);
        int inserted = 0;
        foreach (FenceItemVm item in items)
        {
            if (ContainsSameItem(item))
                continue;
            item.IsDragging = false;
            item.IsSelected = false;
            _items.Insert(Math.Clamp(insert + inserted, 0, _items.Count), item);
            inserted++;
        }

        HideDesktopCounterparts();
        UpdateEmptyHint();
    }

    private int InsertIndexAtScreen(int screenX, int screenY)
    {
        if (_collapsed || !IsVisible)
            return _items.Count;

        try
        {
            System.Windows.Point local = IconGrid.PointFromScreen(new System.Windows.Point(screenX, screenY));
            double panelWidth = IconGrid.ActualWidth > 1 ? IconGrid.ActualWidth : BodyHost.ActualWidth;
            return IconGridReorder.InsertIndex(_items.Count, local.X, local.Y, TileWidth, TileHeight, panelWidth);
        }
        catch (InvalidOperationException)
        {
            return _items.Count;
        }
    }

    internal bool TryGetScreenTarget(out FenceScreenTarget target)
    {
        target = default;
        if (!IsVisible || WindowState == WindowState.Minimized || ActualWidth < 1 || ActualHeight < 1)
            return false;

        try
        {
            System.Drawing.Rectangle window = FenceScreenRect();
            System.Drawing.Rectangle body = _collapsed
                ? new System.Drawing.Rectangle(window.X, window.Y, 0, 0)
                : FenceBodyScreenRect();
            target = new FenceScreenTarget(
                _fenceId,
                window.X,
                window.Y,
                window.Width,
                window.Height,
                body.X,
                body.Y,
                body.Width,
                body.Height,
                _collapsed);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    internal void ShowDropChrome(bool lit) => SetChromeBorder(lit);

    internal void PersistLayout() => SaveLayout();

    internal bool IsRolledUp => _collapsed;

    internal SnapRect LayoutSnapRect()
    {
        double width = Width > 0 ? Width : ActualWidth;
        // ActualHeight: Height pode ainda estar na animação do recolher/expandir.
        double height = ActualHeight > 0 ? ActualHeight : Height;
        return new SnapRect(Left, Top, Math.Max(1, width), Math.Max(1, height));
    }

    internal SnapRect WorkAreaSnapRect()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        MonitorWorkArea.Pixels px = MonitorWorkArea.ForWindow(hwnd);
        if (px.Width <= 0 || px.Height <= 0)
        {
            // Sem HWND/monitor: só o ecrã principal, em DIP.
            Rect work = SystemParameters.WorkArea;
            return new SnapRect(work.X, work.Y, work.Width, work.Height);
        }

        PresentationSource? source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
        {
            Rect work = SystemParameters.WorkArea;
            return new SnapRect(work.X, work.Y, work.Width, work.Height);
        }

        Matrix toDip = source.CompositionTarget.TransformFromDevice;
        System.Windows.Point topLeft = toDip.Transform(new System.Windows.Point(px.X, px.Y));
        System.Windows.Point bottomRight = toDip.Transform(
            new System.Windows.Point(px.X + px.Width, px.Y + px.Height));
        return new SnapRect(
            topLeft.X,
            topLeft.Y,
            Math.Max(1, bottomRight.X - topLeft.X),
            Math.Max(1, bottomRight.Y - topLeft.Y));
    }

    internal void ApplySnapPosition(double x, double y)
    {
        Left = x;
        Top = y;
    }

    internal void ApplySnapRect(SnapRect rect)
    {
        Left = rect.X;
        Top = rect.Y;
        Width = Math.Max(MinWidth, rect.Width);
        if (_collapsed)
            return;

        Height = Math.Max(MinHeight, rect.Height);
        _expandedHeight = Height;
    }

    private static bool SameItem(FenceItemVm left, FenceItemVm right)
    {
        if (!string.IsNullOrWhiteSpace(left.Path)
            && !string.IsNullOrWhiteSpace(right.Path)
            && string.Equals(left.Path, right.Path, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }

    private bool RestoreDesktopItem(FenceItemVm item, int? screenX = null, int? screenY = null)
    {
        if (!_desktop.TryRevealDesktopItem(item.Path, item.Name, item.OriginalPath, out string? restored))
            return false;

        if (!string.IsNullOrEmpty(restored))
            item.Path = restored;
        _desktop.FlushShell();
        TryPlaceRestoredIcon(item, screenX, screenY);
        return true;
    }

    private void TryPlaceRestoredIcon(FenceItemVm item, int? screenX = null, int? screenY = null)
    {
        try
        {
            if (screenX is int x && screenY is int y)
            {
                _desktop.PlaceIconAtScreen(
                    item.Path ?? item.Name, x, y, item.OriginalX, item.OriginalY);
                return;
            }

            if (item.OriginalX is int ox && item.OriginalY is int oy)
                _desktop.SetItemPositionAfterReveal(item.Path ?? item.Name, ox, oy);
        }
        catch
        {
            /* o Explorer volta a desenhar o ícone mesmo sem reposicionar */
        }
    }

    private void ApplyPendingSingleSelect()
    {
        FenceItemVm? item = _pendingSingleSelect;
        _pendingSingleSelect = null;
        if (item is null || _draggingItems is { Count: > 0 })
            return;

        ClearSelection();
        item.IsSelected = true;
        _selected = item;
    }

    private void ClearSelection()
    {
        foreach (FenceItemVm item in _items)
            item.IsSelected = false;
    }

    private List<FenceItemVm> SelectedItems()
    {
        List<FenceItemVm> selected = _items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0 && _selected is not null)
            selected.Add(_selected);
        return selected;
    }

    private void HideDesktopCounterparts()
    {
        bool changed = false;
        foreach (FenceItemVm item in _items)
        {
            DesktopConcealResult result = _desktop.ConcealDesktopItem(
                _fenceId, item.Path, item.Name, item.OriginalPath);
            if (!result.Applied)
                continue;
            if (!string.IsNullOrEmpty(result.OriginalPath) && string.IsNullOrEmpty(item.OriginalPath))
            {
                item.OriginalPath = result.OriginalPath;
                changed = true;
            }

            if (!string.IsNullOrEmpty(result.StoragePath)
                && !string.Equals(item.Path, result.StoragePath, StringComparison.OrdinalIgnoreCase))
            {
                item.Path = result.StoragePath;
                changed = true;
            }
        }

        _desktop.FlushShell();
        if (changed)
            SaveLayout();
    }

    private static void OpenItem(FenceItemVm item)
    {
        string? target = DesktopPaths.ResolveExisting(item.Path ?? item.Name)
                         ?? item.Path
                         ?? DesktopPaths.ResolveExisting(item.Name)
                         ?? item.Name;
        if (string.IsNullOrWhiteSpace(target))
            return;

        bool onDisk = File.Exists(target) || Directory.Exists(target);
        if (!onDisk && (ShellFileIcon.TryExecute(target) || ShellFileIcon.TryExecute(item.Name)))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{target}\"") { UseShellExecute = true });
            }
            catch { }
        }
    }

    private void UpdateEmptyHint()
    {
        EmptyHint.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnUiLanguageChanged() => Dispatcher.Invoke(ApplyChromeStrings);

    private void ApplyChromeStrings()
    {
        MenuToggleCollapse.Header = Loc.T("CollapseExpand");
        MenuDiagnostics.Header = Loc.T("Diagnostics");
        MoveGrip.ToolTip = Loc.T("Drag");
        BtnCollapse.ToolTip = _collapsed ? Loc.T("ExpandBarOnly") : Loc.T("CollapseBarOnly");
        EmptyHint.Text = Loc.T("EmptyHint");
    }

    private void ItemContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu { Items.Count: > 0 } menu && menu.Items[0] is MenuItem item)
            item.Header = Loc.T("RemoveFromFence");
    }

    public string DisplayTitle => CommittedTitle(_renaming ? TitleEdit.Text : TitleDisplay.Text);

    public TitleAlignment CurrentTitleAlignment => _titleAlignment;

    public FenceTheme CurrentTheme => _theme.Normalized();

    public void SetTitleAlignment(TitleAlignment alignment, bool persist = true)
    {
        _titleAlignment = alignment;
        ApplyTitleAlignment();
        if (persist)
            SaveLayout();
    }

    public void SetTheme(FenceTheme theme, bool persist = true)
    {
        _theme = (theme ?? FenceTheme.Default()).Normalized();
        ApplyTheme();
        if (persist)
            SaveLayout();
    }

    public FenceState CaptureState()
    {
        double height = _collapsed
            ? _expandedHeight
            : (ActualHeight > 0 ? ActualHeight : Height);
        if (height <= TitleBarHeight + 8)
            height = Math.Max(DefaultExpandedHeight, _expandedHeight);

        return new FenceState
        {
            Id = _fenceId,
            Title = DisplayTitle,
            TitleAlignment = _titleAlignment,
            Theme = _theme.Normalized(),
            X = Left,
            Y = Top,
            Width = Width,
            Height = height,
            Collapsed = _collapsed,
            Items = _items.Select(i => i.ToState()).ToList()
        };
    }

    private void ApplyTitleAlignment()
    {
        var align = _titleAlignment == TitleAlignment.Center
            ? System.Windows.HorizontalAlignment.Center
            : System.Windows.HorizontalAlignment.Left;
        TitleDisplay.HorizontalAlignment = align;
        TitleEdit.HorizontalAlignment = align;
    }

    private void ApplyTheme()
    {
        FenceTheme theme = _theme.Normalized();
        Chrome.Background = ToBrush(theme.FillArgb);
        TitleBar.Background = ToBrush(theme.HeaderArgb);
        TitleDisplay.Foreground = ToBrush(theme.TextArgb);
        TitleEdit.Foreground = ToBrush(theme.TextArgb);
        TitleEdit.CaretBrush = ToBrush(theme.TextArgb);
        EmptyHint.Foreground = ToBrush(theme.MutedTextArgb);
        MoveGripGlyph.Foreground = ToBrush(theme.GripTextArgb);
        BtnCollapseGlyph.Foreground = ToBrush(theme.CollapseGlyphArgb);
        // ResourceDictionary congela o brush; mutar Color derruba o processo antes de gravar.
        Resources["FenceLabelBrush"] = ToBrush(theme.TextArgb);
        SetChromeBorder(_dropBorderLit);
    }

    private void SetChromeBorder(bool drop)
    {
        _dropBorderLit = drop;
        uint argb = drop ? _theme.Normalized().DropBorderArgb : _theme.Normalized().BorderArgb;
        ChromeStroke.BorderBrush = ToBrush(argb);
    }

    private static SolidColorBrush ToBrush(uint argb) => new(FromArgb(argb));

    private static Color FromArgb(uint argb) =>
        Color.FromArgb(ArgbColor.A(argb), ArgbColor.R(argb), ArgbColor.G(argb), ArgbColor.B(argb));

    private void RestoreLayout()
    {
        try
        {
            FenceState? state = _bootState;
            if (state is null)
            {
                ApplyTitleAlignment();
                ApplyTheme();
                return;
            }

            _titleAlignment = state.TitleAlignment;
            _theme = (state.Theme ?? FenceTheme.Default()).Normalized();
            string title = string.IsNullOrWhiteSpace(state.Title) ? Loc.T("DefaultFenceTitle") : state.Title;
            TitleDisplay.Text = title;
            TitleEdit.Text = title;
            ApplyTitleAlignment();
            ApplyTheme();
            Width = Math.Max(180, state.Width);
            Left = state.X;
            Top = state.Y;
            _expandedHeight = Math.Max(TitleBarHeight + 80, state.Height);

            foreach (FenceItemState item in state.Items)
            {
                string? path = DesktopPaths.ResolveExisting(item.Path ?? item.Name);
                _items.Add(new FenceItemVm
                {
                    Name = item.Name,
                    Path = path ?? item.Path,
                    OriginalPath = item.OriginalPath,
                    Icon = IconImageLoader.Load(path ?? item.Path ?? item.Name),
                    OriginalX = item.OriginalX,
                    OriginalY = item.OriginalY
                });
            }

            if (state.Collapsed)
                Collapse(persist: false, captureExpandedSize: false);
            else
                Height = _expandedHeight;
        }
        catch { }
    }

    private void SaveLayout()
    {
        try { LayoutChanged?.Invoke(); }
        catch { }
    }
}
