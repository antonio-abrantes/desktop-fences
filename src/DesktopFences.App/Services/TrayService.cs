using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DesktopFences.App.Localization;
using Application = System.Windows.Application;

namespace DesktopFences.App.Services;

internal sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private bool _paused;

    public event Action? PauseRequested;
    public event Action? ResumeRequested;
    public event Action? SettingsRequested;
    public event Action? AboutRequested;
    public event Action? ExitRequested;

    public TrayService()
    {
        _icon = new NotifyIcon
        {
            Text = AppInfo.ProductName,
            Visible = true,
            Icon = LoadIcon()
        };

        _icon.ContextMenuStrip = BuildMenu();
        _icon.DoubleClick += (_, _) =>
        {
            if (_paused)
                ResumeRequested?.Invoke();
            else
                SettingsRequested?.Invoke();
        };
        UiLocale.Changed += OnLanguageChanged;
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        ApplyChrome();
    }

    private void OnLanguageChanged() => ApplyChrome();

    private void ApplyChrome()
    {
        _icon.Text = _paused ? Loc.T("TrayPaused") : AppInfo.ProductName;
        RebuildMenu();
    }

    private void RebuildMenu()
    {
        ContextMenuStrip? old = _icon.ContextMenuStrip;
        _icon.ContextMenuStrip = BuildMenu();
        old?.Dispose();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        if (_paused)
            menu.Items.Add(Loc.T("Resume"), null, (_, _) => ResumeRequested?.Invoke());
        else
            menu.Items.Add(Loc.T("Pause"), null, (_, _) => PauseRequested?.Invoke());

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Loc.T("Settings"), null, (_, _) => SettingsRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Loc.T("About"), null, (_, _) => AboutRequested?.Invoke());
        menu.Items.Add(Loc.T("Exit"), null, (_, _) => ExitRequested?.Invoke());
        return menu;
    }

    private static Icon LoadIcon()
    {
        try
        {
            Stream? stream = Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/app.ico"))?.Stream;
            if (stream is not null)
            {
                using (stream)
                    return new Icon(stream, 16, 16);
            }
        }
        catch { }

        try
        {
            string? path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
            {
                Icon? extracted = Icon.ExtractAssociatedIcon(path);
                if (extracted is not null)
                    return extracted;
            }
        }
        catch { }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        UiLocale.Changed -= OnLanguageChanged;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
