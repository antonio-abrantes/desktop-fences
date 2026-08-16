using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace DesktopFences.Recovery;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new Mutex(
            initiallyOwned: true,
            @"Local\DesktopFences.SingleInstance",
            out bool created);
        if (!created)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            MessageBox.Show(
                RecoveryText.AppMustBeClosed,
                RecoveryText.Title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        MainWindow = new MainWindow();
        MainWindow.Closed += (_, _) => Shutdown();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_singleInstance is not null)
        {
            try { _singleInstance.ReleaseMutex(); } catch { }
            _singleInstance.Dispose();
        }
        base.OnExit(e);
    }
}
