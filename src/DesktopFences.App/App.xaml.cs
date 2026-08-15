using System.Windows;
using DesktopFences.App.Services;
using Application = System.Windows.Application;

namespace DesktopFences.App;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private TrayService? _tray;
    private FenceHost? _host;
    private SettingsWindow? _settings;
    private bool _paused;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _singleInstance = new Mutex(initiallyOwned: true, @"Local\DesktopFences.SingleInstance", out bool created);
        if (!created)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        StartupRegistration.RefreshPathIfEnabled();

        _host = new FenceHost();
        _host.Start();
        MainWindow = _host.Windows.FirstOrDefault();

        _tray = new TrayService();
        _tray.PauseRequested += Pause;
        _tray.ResumeRequested += Resume;
        _tray.SettingsRequested += OpenSettings;
        _tray.AboutRequested += OpenAbout;
        _tray.ExitRequested += ExitApp;
    }

    private void Pause()
    {
        _paused = true;
        _host?.PauseAll();
        _tray?.SetPaused(true);
    }

    private void Resume()
    {
        _paused = false;
        _host?.ResumeAll();
        _tray?.SetPaused(false);
    }

    private void OpenSettings()
    {
        if (_host is null)
            return;

        Dispatcher.Invoke(() =>
        {
            if (_settings is null)
            {
                _settings = new SettingsWindow(_host);
                _settings.Closed += (_, _) => _settings = null;
                _settings.Show();
            }

            _settings.Show();
            _settings.Activate();
            _settings.WindowState = WindowState.Normal;
        });
    }

    private void OpenAbout()
    {
        Dispatcher.Invoke(AboutWindow.ShowOrActivate);
    }

    private void ExitApp()
    {
        _host?.PrepareExit();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (!_paused)
            _host?.RestoreAllIcons();

        _tray?.Dispose();
        if (_singleInstance is not null)
        {
            try { _singleInstance.ReleaseMutex(); }
            catch { }
            _singleInstance.Dispose();
        }

        base.OnExit(e);
    }
}
