using System.ComponentModel;
using System.Windows;
using DesktopFences.Native;
using MessageBox = System.Windows.MessageBox;

namespace DesktopFences.Recovery;

public partial class MainWindow : Window
{
    private bool _recovering;

    public MainWindow()
    {
        InitializeComponent();
        HeadingText.Text = RecoveryText.Heading;
        ExplanationText.Text = RecoveryText.Explanation;
        WarningText.Text = RecoveryText.Warning;
        RestorePositionsCheckBox.Content = RecoveryText.RestorePositionsOption;
        RestoreButton.Content = RecoveryText.RestoreButton;
        CloseButton.Content = RecoveryText.CloseButton;
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                RecoveryText.Confirm,
                RecoveryText.Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        RestoreButton.IsEnabled = false;
        CloseButton.IsEnabled = false;
        _recovering = true;
        StatusText.Text = RecoveryText.Restoring;
        try
        {
            bool restorePositions = RestorePositionsCheckBox.IsChecked == true;
            EmergencyRecoveryReport report = await Task.Run(
                () => new EmergencyRecoveryService().RestoreAll(restorePositions));
            if (!report.Success)
            {
                string error = string.Join(Environment.NewLine, report.Errors);
                StatusText.Text = RecoveryText.Failed(error);
                MessageBox.Show(StatusText.Text, RecoveryText.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StatusText.Text = RecoveryText.Completed(
                report.CopiedFiles,
                report.CopiedDirectories,
                report.PositionsRestored,
                report.ConflictsPreserved,
                report.RecoverySessionPath,
                restorePositions);
            MessageBox.Show(StatusText.Text, RecoveryText.Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText.Text = RecoveryText.Failed(ex.Message);
            MessageBox.Show(StatusText.Text, RecoveryText.Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _recovering = false;
            RestoreButton.IsEnabled = true;
            CloseButton.IsEnabled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => System.Windows.Application.Current.Shutdown();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_recovering)
            e.Cancel = true;
        base.OnClosing(e);
    }
}
