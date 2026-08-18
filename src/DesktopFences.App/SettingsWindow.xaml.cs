using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DesktopFences.App.Localization;
using DesktopFences.App.Services;
using DesktopFences.Core;
using DesktopFences.Core.Fences;
using DesktopFences.Core.Models;
using DesktopFences.Core.Persistence;
using ColorDialog = System.Windows.Forms.ColorDialog;
using WinColor = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;
using IWin32Window = System.Windows.Forms.IWin32Window;

namespace DesktopFences.App;

public partial class SettingsWindow : Window
{
    private readonly FenceHost _host;
    private readonly ObservableCollection<FenceRow> _rows = [];
    private bool _syncing;

    public SettingsWindow(FenceHost host)
    {
        InitializeComponent();
        _host = host;
        FenceList.ItemsSource = _rows;
        _host.FencesChanged += OnFencesChanged;
        UiLocale.Changed += OnLanguageChanged;
        ApplyStrings();
        Loaded += (_, _) =>
        {
            SyncStartup();
            SyncLanguageCombo();
            Reload(selectFirst: true);
        };
        Closed += (_, _) =>
        {
            _host.FencesChanged -= OnFencesChanged;
            UiLocale.Changed -= OnLanguageChanged;
        };
    }

    private void OnFencesChanged()
    {
        Dispatcher.Invoke(() => Reload(selectFirst: false));
    }

    private void OnLanguageChanged()
    {
        Dispatcher.Invoke(() =>
        {
            ApplyStrings();
            Reload(selectFirst: false);
        });
    }

    private void ApplyStrings()
    {
        Title = Loc.T("SettingsTitle");
        TxtHeading.Text = Loc.T("SettingsHeading");
        TxtTitleSuffix.Text = Loc.T("SettingsTitleSuffix");
        BtnAbout.ToolTip = Loc.T("About");
        BtnClose.ToolTip = Loc.T("CloseTooltip");
        BtnNewFence.Content = Loc.T("NewFence");
        BtnRemove.Content = Loc.T("Remove");
        BtnRemove.ToolTip = Loc.T("RemoveLastTooltip");
        BtnResetTheme.Content = Loc.T("ResetTheme");
        BtnResetTheme.ToolTip = Loc.T("ResetThemeTooltip");
        BtnSetDefaultLayout.Content = Loc.T("SetDefaultLayout");
        BtnSetDefaultLayout.ToolTip = Loc.T("SetDefaultLayoutTooltip");
        StartWithWindows.Content = Loc.T("StartWithWindows");
        StartWithWindows.ToolTip = Loc.T("StartWithWindowsTooltip");
        TxtStartupHint.Text = Loc.T("StartupHint");
        TxtLanguage.Text = Loc.T("Language");
        SetComboItem(0, Loc.T("LanguageSystem"));
        SetComboItem(1, Loc.T("LanguagePortuguese"));
        SetComboItem(2, Loc.T("LanguageEnglish"));
        TxtFolders.Text = Loc.T("FoldersSection");
        BtnOpenLayout.Content = Loc.T("OpenLayout");
        BtnOpenLayout.ToolTip = Loc.T("OpenLayoutTooltip");
        BtnOpenItems.Content = Loc.T("OpenItems");
        BtnOpenItems.ToolTip = Loc.T("OpenItemsTooltip");
        TxtLayoutPath.Text = LayoutStore.DefaultPath();
        TxtItemsPath.Text = FenceItemStore.Root();
        TxtFenceListLabel.Text = Loc.T("FenceListLabel");
        ApplyAllCheck.Content = Loc.T("ApplyAll");
        ApplyAllCheck.ToolTip = Loc.T("ApplyAllTooltip");
        TxtApplyAllHint.Text = Loc.T("ApplyAllHint");
        TxtAlignLabel.Text = Loc.T("TitleAlignment");
        AlignLeft.Content = Loc.T("AlignLeft");
        AlignCenter.Content = Loc.T("AlignCenter");
        TxtAppearance.Text = Loc.T("Appearance");
        TxtFill.Text = Loc.T("Fill");
        TxtBorder.Text = Loc.T("Border");
        TxtHeader.Text = Loc.T("Header");
        TxtText.Text = Loc.T("Text");
        SwatchFill.ToolTip = Loc.T("FillTooltip");
        SwatchBorder.ToolTip = Loc.T("BorderTooltip");
        SwatchHeader.ToolTip = Loc.T("HeaderTooltip");
        SwatchText.ToolTip = Loc.T("TextTooltip");
        SyncLanguageCombo();
    }

    private void SetComboItem(int index, string text)
    {
        if (CboLanguage.Items[index] is ComboBoxItem item)
            item.Content = text;
    }

    private void SyncLanguageCombo()
    {
        _syncing = true;
        foreach (object entry in CboLanguage.Items)
        {
            if (entry is ComboBoxItem item && item.Tag as string == _host.UiLanguage)
            {
                CboLanguage.SelectedItem = item;
                break;
            }
        }
        _syncing = false;
    }

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || CboLanguage.SelectedItem is not ComboBoxItem { Tag: string code })
            return;
        _host.SetUiLanguage(code);
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void About_Click(object sender, RoutedEventArgs e) => AboutWindow.ShowOrActivate();

    private void OpenLayout_Click(object sender, RoutedEventArgs e) =>
        OpenInExplorer(LayoutStore.DefaultPath(), selectFile: true);

    private void OpenItems_Click(object sender, RoutedEventArgs e) =>
        OpenInExplorer(FenceItemStore.Root(), selectFile: false);

    private static void OpenInExplorer(string path, bool selectFile)
    {
        try
        {
            string? folder = selectFile ? Path.GetDirectoryName(path) : path;
            if (string.IsNullOrEmpty(folder))
                return;
            Directory.CreateDirectory(folder);

            string args = selectFile && File.Exists(path)
                ? $"/select,\"{path}\""
                : $"\"{folder}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
        }
        catch
        {
            /* pasta em falta / Explorador recusou */
        }
    }

    private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncing)
            return;
        StartupRegistration.SetEnabled(StartWithWindows.IsChecked == true);
        SyncStartup();
    }

    private void SyncStartup()
    {
        _syncing = true;
        StartWithWindows.IsChecked = StartupRegistration.IsEnabled();
        _syncing = false;
    }

    private void AddFence_Click(object sender, RoutedEventArgs e)
    {
        _host.TryAddNew();
        Reload(selectLast: true);
    }

    private async void RemoveFence_Click(object sender, RoutedEventArgs e)
    {
        if (_host.IsReturningItemsToDesktop || FenceList.SelectedItem is not FenceRow row)
            return;

        int itemCount = _host.Windows.FirstOrDefault(w => w.FenceId == row.Id)?.Items.Count ?? 0;
        if (!ConfirmRemoveFence(row.Title, itemCount))
            return;

        SetReturningOverlay(itemCount > 0, itemCount);
        try
        {
            if (!await _host.TryRemoveAsync(row.Id).ConfigureAwait(true))
                return;
            Reload(selectFirst: true);
        }
        finally
        {
            SetReturningOverlay(false, 0);
            UpdateRemoveEnabled();
        }
    }

    private void SetReturningOverlay(bool visible, int itemCount)
    {
        ReturnOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (!visible)
            return;

        TxtReturnOverlay.Text = itemCount == 1
            ? Loc.T("ReturningOneItemToDesktop")
            : Loc.Format("ReturningItemsToDesktop", itemCount);
        BtnNewFence.IsEnabled = false;
        BtnRemove.IsEnabled = false;
        BtnResetTheme.IsEnabled = false;
        BtnSetDefaultLayout.IsEnabled = false;
        FenceList.IsEnabled = false;
    }

    private bool ConfirmRemoveFence(string title, int itemCount)
    {
        string message = itemCount switch
        {
            0 => Loc.Format("RemoveFenceConfirmEmpty", title),
            1 => Loc.Format("RemoveFenceConfirmOneItem", title),
            _ => Loc.Format("RemoveFenceConfirmItems", itemCount, title)
        };

        return System.Windows.MessageBox.Show(
            this,
            message,
            Loc.T("RemoveFenceConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    private bool ApplyToAll => ApplyAllCheck.IsChecked == true;

    private void ResetTheme_Click(object sender, RoutedEventArgs e)
    {
        if (ApplyToAll)
        {
            _host.ResetThemeAll();
            SyncAppearance();
            return;
        }

        if (FenceList.SelectedItem is not FenceRow row)
            return;
        _host.ResetTheme(row.Id);
        SyncAppearance();
    }

    private void SetDefaultLayout_Click(object sender, RoutedEventArgs e)
    {
        if (FenceList.SelectedItem is not FenceRow row)
            return;

        _host.SetDefaultAppearanceFromFence(row.Id);
    }

    private void FenceList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        SyncAlignmentRadios();
        SyncAppearance();
        UpdateRemoveEnabled();
    }

    private void AlignLeft_Checked(object sender, RoutedEventArgs e) => CommitAlignment(TitleAlignment.Left);

    private void AlignCenter_Checked(object sender, RoutedEventArgs e) => CommitAlignment(TitleAlignment.Center);

    private void ApplyAllCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncing || FenceList.SelectedItem is not FenceRow row)
            return;
        if (!ApplyToAll)
            return;

        TitleAlignment alignment = AlignCenter.IsChecked == true
            ? TitleAlignment.Center
            : TitleAlignment.Left;
        _host.SetTitleAlignmentAll(alignment);
        _host.SetThemeAll(_host.GetTheme(row.Id));
    }

    private void CommitAlignment(TitleAlignment alignment)
    {
        if (_syncing)
            return;

        if (ApplyToAll)
        {
            _host.SetTitleAlignmentAll(alignment);
            return;
        }

        if (FenceList.SelectedItem is not FenceRow row)
            return;
        _host.SetTitleAlignment(row.Id, alignment);
    }

    private void CommitTheme(FenceTheme theme)
    {
        if (ApplyToAll)
        {
            _host.SetThemeAll(theme);
            return;
        }

        if (FenceList.SelectedItem is not FenceRow row)
            return;
        _host.SetTheme(row.Id, theme);
    }

    private void SwatchFill_Click(object sender, MouseButtonEventArgs e) => PickRgb(ThemeChannel.Fill);
    private void SwatchBorder_Click(object sender, MouseButtonEventArgs e) => PickRgb(ThemeChannel.Border);
    private void SwatchHeader_Click(object sender, MouseButtonEventArgs e) => PickRgb(ThemeChannel.Header);
    private void SwatchText_Click(object sender, MouseButtonEventArgs e) => PickRgb(ThemeChannel.Text);

    private void SliderFill_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || FenceList.SelectedItem is not FenceRow row)
            return;
        FenceTheme theme = _host.GetTheme(row.Id);
        byte a = ArgbColor.Clamp(ArgbColor.AlphaFromPercent((int)Math.Round(SliderFill.Value)), FenceTheme.FillAlphaMin, FenceTheme.FillAlphaMax);
        theme.Fill = ArgbColor.ToHex(ArgbColor.WithAlpha(theme.FillArgb, a));
        CommitTheme(theme);
        LblFillPct.Text = $"{ArgbColor.PercentFromAlpha(a)}%";
        PaintSwatch(SwatchFill, theme.FillArgb);
    }

    private void SliderBorder_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || FenceList.SelectedItem is not FenceRow row)
            return;
        FenceTheme theme = _host.GetTheme(row.Id);
        byte a = ArgbColor.Clamp(ArgbColor.AlphaFromPercent((int)Math.Round(SliderBorder.Value)), FenceTheme.BorderAlphaMin, FenceTheme.BorderAlphaMax);
        theme.Border = ArgbColor.ToHex(ArgbColor.WithAlpha(theme.BorderArgb, a));
        CommitTheme(theme);
        LblBorderPct.Text = $"{ArgbColor.PercentFromAlpha(a)}%";
        PaintSwatch(SwatchBorder, theme.BorderArgb);
    }

    private void SliderHeader_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || FenceList.SelectedItem is not FenceRow row)
            return;
        FenceTheme theme = _host.GetTheme(row.Id);
        byte a = ArgbColor.Clamp(ArgbColor.AlphaFromPercent((int)Math.Round(SliderHeader.Value)), FenceTheme.HeaderAlphaMin, FenceTheme.HeaderAlphaMax);
        theme.Header = ArgbColor.ToHex(ArgbColor.WithAlpha(theme.HeaderArgb, a));
        CommitTheme(theme);
        LblHeaderPct.Text = $"{ArgbColor.PercentFromAlpha(a)}%";
        PaintSwatch(SwatchHeader, theme.HeaderArgb);
    }

    private void PickRgb(ThemeChannel channel)
    {
        if (FenceList.SelectedItem is not FenceRow row)
            return;

        FenceTheme theme = _host.GetTheme(row.Id);
        uint current = channel switch
        {
            ThemeChannel.Fill => theme.FillArgb,
            ThemeChannel.Border => theme.BorderArgb,
            ThemeChannel.Header => theme.HeaderArgb,
            _ => theme.TextArgb
        };

        if (!TryPickRgb(current, out uint rgb))
            return;

        switch (channel)
        {
            case ThemeChannel.Fill:
                theme.Fill = ArgbColor.ToHex(ArgbColor.WithRgb(theme.FillArgb, rgb));
                break;
            case ThemeChannel.Border:
                theme.Border = ArgbColor.ToHex(ArgbColor.WithRgb(theme.BorderArgb, rgb));
                break;
            case ThemeChannel.Header:
                uint header = ArgbColor.WithRgb(theme.HeaderArgb, rgb);
                byte minVisible = ArgbColor.AlphaFromPercent(45);
                if (ArgbColor.A(header) < minVisible)
                    header = ArgbColor.WithAlpha(header, minVisible);
                theme.Header = ArgbColor.ToHex(header);
                break;
            default:
                theme.Text = ArgbColor.ToHex(ArgbColor.WithRgb(theme.TextArgb, rgb));
                break;
        }

        CommitTheme(theme);
        SyncAppearance();
    }

    private bool TryPickRgb(uint currentArgb, out uint rgb)
    {
        rgb = ArgbColor.Rgb(currentArgb);
        using var dlg = new ColorDialog
        {
            Color = WinColor.FromArgb(ArgbColor.R(currentArgb), ArgbColor.G(currentArgb), ArgbColor.B(currentArgb)),
            FullOpen = true,
            AnyColor = true,
            SolidColorOnly = true
        };

        var owner = new DialogOwner(new WindowInteropHelper(this).EnsureHandle());
        if (dlg.ShowDialog(owner) != System.Windows.Forms.DialogResult.OK)
            return false;

        WinColor c = dlg.Color;
        rgb = ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
        return true;
    }

    private void Reload(bool selectFirst = false, bool selectLast = false)
    {
        Guid? keep = (FenceList.SelectedItem as FenceRow)?.Id;
        _rows.Clear();
        foreach (FenceSummary item in _host.Summaries())
            _rows.Add(new FenceRow(item.Id, item.Title, item.TitleAlignment));

        if (_rows.Count == 0)
            return;

        FenceRow? pick = null;
        if (selectLast)
            pick = _rows[^1];
        else if (selectFirst)
            pick = _rows[0];
        else if (keep is Guid id)
            pick = _rows.FirstOrDefault(r => r.Id == id) ?? _rows[0];

        _syncing = true;
        FenceList.SelectedItem = pick;
        _syncing = false;
        SyncAlignmentRadios();
        SyncAppearance();
        UpdateRemoveEnabled();
    }

    private void SyncAlignmentRadios()
    {
        _syncing = true;
        if (FenceList.SelectedItem is FenceRow row)
        {
            AlignLeft.IsChecked = row.Alignment == TitleAlignment.Left;
            AlignCenter.IsChecked = row.Alignment == TitleAlignment.Center;
        }
        _syncing = false;
    }

    private void SyncAppearance()
    {
        if (FenceList.SelectedItem is not FenceRow row)
            return;

        FenceTheme theme = _host.GetTheme(row.Id).Normalized();
        _syncing = true;
        SliderFill.Value = Math.Clamp(ArgbColor.PercentFromAlpha(ArgbColor.A(theme.FillArgb)), 45, 85);
        SliderBorder.Value = Math.Clamp(ArgbColor.PercentFromAlpha(ArgbColor.A(theme.BorderArgb)), 15, 100);
        SliderHeader.Value = Math.Clamp(ArgbColor.PercentFromAlpha(ArgbColor.A(theme.HeaderArgb)), 15, 85);
        LblFillPct.Text = $"{ArgbColor.PercentFromAlpha(ArgbColor.A(theme.FillArgb))}%";
        LblBorderPct.Text = $"{ArgbColor.PercentFromAlpha(ArgbColor.A(theme.BorderArgb))}%";
        LblHeaderPct.Text = $"{ArgbColor.PercentFromAlpha(ArgbColor.A(theme.HeaderArgb))}%";
        PaintSwatch(SwatchFill, theme.FillArgb);
        PaintSwatch(SwatchBorder, theme.BorderArgb);
        PaintSwatch(SwatchHeader, theme.HeaderArgb);
        PaintSwatch(SwatchText, theme.TextArgb);
        _syncing = false;
    }

    private static void PaintSwatch(System.Windows.Controls.Border swatch, uint argb)
    {
        byte a = ArgbColor.A(argb);
        swatch.Background = new SolidColorBrush(MediaColor.FromArgb(
            a < 80 ? (byte)255 : a,
            ArgbColor.R(argb),
            ArgbColor.G(argb),
            ArgbColor.B(argb)));
    }

    private void UpdateRemoveEnabled()
    {
        bool busy = _host.IsReturningItemsToDesktop;
        BtnRemove.IsEnabled = !busy && FenceLayoutRules.CanRemove(_rows.Count) && FenceList.SelectedItem is FenceRow;
        BtnNewFence.IsEnabled = !busy;
        BtnResetTheme.IsEnabled = !busy;
        BtnSetDefaultLayout.IsEnabled = !busy;
        FenceList.IsEnabled = !busy;
    }

    private enum ThemeChannel { Fill, Border, Header, Text }

    private sealed class DialogOwner(IntPtr handle) : IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }

    private sealed class FenceRow(Guid id, string title, TitleAlignment alignment)
    {
        public Guid Id { get; } = id;
        public string Title { get; } = title;
        public TitleAlignment Alignment { get; } = alignment;
        public string AlignmentLabel => Alignment == TitleAlignment.Center
            ? Loc.T("AlignCenterShort")
            : Loc.T("AlignLeftShort");
    }
}
