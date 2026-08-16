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
            System.Windows.MessageBox.Show(
                "O DesktopFences já está a correr (ícone na bandeja). Fecha-o aí antes de abrir outra vez.",
                "DesktopFences",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        StartupRegistration.RefreshPathIfEnabled();

        _host = new FenceHost();
        try
        {
            _host.Start();
        }
        catch (Exception ex)
        {
            _host.RestoreAllIcons();
            System.Windows.MessageBox.Show(
                $"O DesktopFences não pôde recuperar os itens com segurança e não será iniciado.\n\n{ex.Message}\n\nDados preservados em: {DesktopFences.Core.FenceItemStore.Root()}",
                "DesktopFences",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }
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
        if (_host is null || !_host.PauseAll())
            return;
        _paused = true;
        _tray?.SetPaused(true);
    }

    private void Resume()
    {
        if (_host is null || !_host.ResumeAll())
            return;
        _paused = false;
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
        if (_host is not null && !_host.PrepareExit())
            return;
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
