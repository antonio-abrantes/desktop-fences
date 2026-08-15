using DesktopFences.Core.Process;
using Microsoft.Win32;

namespace DesktopFences.App.Services;

/// <summary>
/// HKCU Run — inicia com a sessão do utilizador, sem admin.
/// Path = o .exe que está a correr (portable: ao abrir noutro sítio, atualiza o atalho).
/// </summary>
internal static class StartupRegistration
{
    internal const string ValueName = "DesktopFences";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? run = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            if (run?.GetValue(ValueName) is not string command || string.IsNullOrWhiteSpace(command))
                return false;
            return !IsDisabledByWindows();
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
                WriteCurrentPath(enableApproved: true);
            else
                Remove();
        }
        catch
        {
            // Falha de registry não pode derrubar o app.
        }
    }

    /// <summary>
    /// Se o arranque está ligado, regrava o path do .exe atual.
    /// Assim o portable acompanha a pasta para onde o utilizador moveu o programa.
    /// </summary>
    public static void RefreshPathIfEnabled()
    {
        if (IsEnabled())
            WriteCurrentPath(enableApproved: false);
    }

    private static void WriteCurrentPath(bool enableApproved)
    {
        string? path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        using RegistryKey run = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        run.SetValue(ValueName, CommandLinePath.Quote(path));

        if (enableApproved)
            WriteApprovedEnabled();
    }

    private static void Remove()
    {
        using (RegistryKey? run = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
            run?.DeleteValue(ValueName, throwOnMissingValue: false);

        using RegistryKey? approved = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath, writable: true);
        approved?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static bool IsDisabledByWindows()
    {
        using RegistryKey? approved = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath);
        if (approved?.GetValue(ValueName) is not byte[] data || data.Length == 0)
            return false;
        return data[0] == 0x03;
    }

    private static void WriteApprovedEnabled()
    {
        using RegistryKey approved = Registry.CurrentUser.CreateSubKey(ApprovedKeyPath);
        approved.SetValue(ValueName, new byte[]
        {
            0x02, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        });
    }
}
