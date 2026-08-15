using System.Globalization;
using DesktopFences.Core.Fences;

namespace DesktopFences.App.Localization;

internal static class UiLocale
{
    private static readonly CultureInfo OsUiCulture = CultureInfo.CurrentUICulture;
    private static readonly CultureInfo Portuguese = new("pt-BR");
    private static readonly CultureInfo English = new("en");

    private static string _preference = UiLanguageCodes.System;

    public static event Action? Changed;

    public static string Preference => _preference;

    public static CultureInfo Culture { get; private set; } = ResolveCulture(UiLanguageCodes.System);

    public static void Apply(string? preference)
    {
        string next = UiLanguageCodes.Normalize(preference);
        CultureInfo culture = ResolveCulture(next);
        bool changed = next != _preference || !Equals(Culture.Name, culture.Name);
        _preference = next;
        Culture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        if (changed)
            Changed?.Invoke();
    }

    public static CultureInfo ResolveCulture(string preference)
    {
        return UiLanguageCodes.Normalize(preference) switch
        {
            UiLanguageCodes.Portuguese => Portuguese,
            UiLanguageCodes.English => English,
            _ => OsLooksPortuguese() ? Portuguese : English
        };
    }

    private static bool OsLooksPortuguese() =>
        OsUiCulture.TwoLetterISOLanguageName.Equals("pt", StringComparison.OrdinalIgnoreCase);
}
