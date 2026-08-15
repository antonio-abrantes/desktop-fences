using System.Windows;
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
        TxtStatus.Text = $"Handle {snapshot.HandleHex}  |  {_all.Count} ícones.";
    }

    private void BtnMoveTest_Click(object sender, RoutedEventArgs e)
    {
        if (_all.Count == 0)
        {
            TxtStatus.Text = "Clique em 'Ler todos os ícones' primeiro.";
            return;
        }

        DesktopIcon first = _all[0];
        _originalFirst = (first.X, first.Y);
        _icons.SetItemPosition(first.Index, first.X + 80, first.Y + 80);
        TxtStatus.Text = $"Movi '{first.Name}' para ({first.X + 80},{first.Y + 80}). Confira no desktop.";
    }

    private void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        if (_originalFirst is null || _all.Count == 0)
        {
            TxtStatus.Text = "Nada para restaurar.";
            return;
        }

        var (x, y) = _originalFirst.Value;
        _icons.SetItemPosition(_all[0].Index, x, y);
        TxtStatus.Text = $"Restaurado para ({x},{y}).";
        _originalFirst = null;
    }
}
