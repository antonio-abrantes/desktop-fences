using DesktopFences.Core.Fences;

namespace DesktopFences.App;

internal enum InstallerMaintenanceMode
{
    Finalize,
    Keep,
    UpgradeKeep,
    Reset,
    UninstallKeep,
    Remove
}

internal sealed record InstallerMaintenanceArguments(
    InstallerMaintenanceMode Mode,
    string? Language)
{
    public static bool TryParse(
        IReadOnlyList<string> args,
        out InstallerMaintenanceArguments? result,
        out string? error)
    {
        result = null;
        error = null;
        string? modeValue = args.FirstOrDefault(arg =>
            arg.StartsWith("--maintenance=", StringComparison.OrdinalIgnoreCase));
        if (modeValue is null)
            return false;

        string rawMode = modeValue[(modeValue.IndexOf('=') + 1)..].Trim();
        if (!Enum.TryParse(rawMode, ignoreCase: true, out InstallerMaintenanceMode mode))
        {
            error = $"Modo de manutenção desconhecido: {rawMode}";
            return true;
        }

        string? languageValue = args.FirstOrDefault(arg =>
            arg.StartsWith("--language=", StringComparison.OrdinalIgnoreCase));
        string? language = languageValue?[(languageValue.IndexOf('=') + 1)..].Trim();
        if (!string.IsNullOrWhiteSpace(language)
            && language is not UiLanguageCodes.Portuguese and not UiLanguageCodes.English)
        {
            error = $"Idioma de instalação desconhecido: {language}";
            return true;
        }

        result = new InstallerMaintenanceArguments(mode, language);
        return true;
    }
}
