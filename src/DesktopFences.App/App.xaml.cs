using System.Windows;
using DesktopFences.App.Services;
using Application = System.Windows.Application;

namespace DesktopFences.App;

public partial class App : Application
{
    private TrayService? _tray;
    private FenceWindow? _fence;
    private bool _paused;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _fence = new FenceWindow();
        MainWindow = _fence;
        _fence.CloseToTray += Pause;
        _fence.Show();

        _tray = new TrayService();
        _tray.PauseRequested += Pause;
        _tray.ResumeRequested += Resume;
        _tray.ExitRequested += ExitApp;
    }

    private void Pause()
    {
        _paused = true;
        _fence?.Pause();
        _tray?.SetPaused(true);
    }

    private void Resume()
    {
        _paused = false;
        _fence?.Resume();
        _tray?.SetPaused(false);
    }

    private void ExitApp()
    {
        _fence?.RestoreHiddenIcons();
        _fence?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (!_paused)
            _fence?.RestoreHiddenIcons();

        _tray?.Dispose();
        base.OnExit(e);
    }
}
