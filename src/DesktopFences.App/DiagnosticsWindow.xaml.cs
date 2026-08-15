using System.Windows;
using DesktopFences.App.Localization;
using DesktopFences.Core.Models;
using DesktopFences.Native;

namespace DesktopFences.App;

public partial class DiagnosticsWindow : Window
{
    private readonly DesktopIconService _icons;
    private IReadOnlyList<DesktopIcon> _all = [];
    private (int X, int Y)? _originalFirst;

    public DiagnosticsWindow(DesktopIconService icons)
    {
        _icons = icons;
        InitializeComponent();
        UiLocale.Changed += OnLanguageChanged;
        Closed += (_, _) => UiLocale.Changed -= OnLanguageChanged;
        ApplyStrings();
    }

    private void OnLanguageChanged() => Dispatcher.Invoke(ApplyStrings);

    private void ApplyStrings()
    {
        Title = Loc.T("DiagnosticsTitle");
        BtnRefresh.Content = Loc.T("DiagReadAll");
        BtnMoveTest.Content = Loc.T("DiagMoveFirst");
        BtnRestore.Content = Loc.T("DiagRestoreFirst");
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        DesktopSnapshot snapshot = _icons.Capture();
        if (!snapshot.Connected)
        {
            TxtStatus.Text = snapshot.Error;
            ListIcons.ItemsSource = null;
            return;
        }

        _all = snapshot.Icons;
        ListIcons.ItemsSource = _all.Select(i => $"[{i.Index}] {i.Name} ({i.X},{i.Y})").ToList();
        TxtStatus.Text = Loc.Format("DiagStatus", snapshot.HandleHex, _all.Count);
    }

    private void BtnMoveTest_Click(object sender, RoutedEventArgs e)
    {
        if (_all.Count == 0)
        {
            TxtStatus.Text = Loc.T("DiagNeedReadFirst");
            return;
        }

        DesktopIcon first = _all[0];
        _originalFirst = (first.X, first.Y);
        _icons.SetItemPosition(first.Index, first.X + 80, first.Y + 80);
        TxtStatus.Text = Loc.Format("DiagMoved", first.Name, first.X + 80, first.Y + 80);
    }

    private void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        if (_originalFirst is null || _all.Count == 0)
        {
            TxtStatus.Text = Loc.T("DiagNothingToRestore");
            return;
        }

        var (x, y) = _originalFirst.Value;
        _icons.SetItemPosition(_all[0].Index, x, y);
        TxtStatus.Text = Loc.Format("DiagRestored", x, y);
        _originalFirst = null;
    }
}
