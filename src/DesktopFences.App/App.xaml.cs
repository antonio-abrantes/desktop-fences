using System.Windows;
using System.Diagnostics;
using System.IO;
using DesktopFences.App.Services;
using Application = System.Windows.Application;

namespace DesktopFences.App;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private TrayService? _tray;
    private FenceHost? _host;
    private SettingsWindow? _settings;
    private MaintenancePipeServer? _maintenancePipe;
    private bool _paused;
    private bool _startRecoveryOnExit;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (InstallerMaintenanceArguments.TryParse(e.Args, out InstallerMaintenanceArguments? maintenance, out string? error))
        {
            Environment.ExitCode = maintenance is null || error is not null
                ? 2
                : InstallerMaintenance.Run(maintenance);
            Shutdown(Environment.ExitCode);
            return;
        }

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
            MessageBoxResult choice = System.Windows.MessageBox.Show(
                $"O DesktopFences não pôde recuperar os itens com segurança e não será iniciado.\n\n{ex.Message}\n\nDados preservados em: {DesktopFences.Core.FenceItemStore.Root()}\n\nDeseja abrir a recuperação de emergência?",
                "DesktopFences",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);
            _startRecoveryOnExit = choice == MessageBoxResult.Yes;
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
        _maintenancePipe = new MaintenancePipeServer(
            Dispatcher,
            () => _host?.PrepareExit() == true,
            () => Shutdown());
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
        _maintenancePipe?.Dispose();
        _maintenancePipe = null;
        if (!_paused)
            _host?.RestoreAllIcons();

        _tray?.Dispose();
        if (_singleInstance is not null)
        {
            try { _singleInstance.ReleaseMutex(); }
            catch { }
            _singleInstance.Dispose();
        }

        if (_startRecoveryOnExit)
        {
            try
            {
                string? directory = Path.GetDirectoryName(Environment.ProcessPath);
                string recovery = Path.Combine(directory ?? string.Empty, "DesktopFences.Recovery.exe");
                if (File.Exists(recovery))
                    Process.Start(new ProcessStartInfo(recovery) { UseShellExecute = true });
            }
            catch { }
        }

        base.OnExit(e);
    }
}
