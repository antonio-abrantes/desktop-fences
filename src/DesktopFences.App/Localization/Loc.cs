using System.Resources;

namespace DesktopFences.App.Localization;

internal static class Loc
{
    private static readonly ResourceManager Manager =
        new("DesktopFences.App.Localization.Strings", typeof(Loc).Assembly);

    public static string T(string name) =>
        Manager.GetString(name, UiLocale.Culture) ?? name;

    public static string Format(string name, params object[] args) =>
        string.Format(UiLocale.Culture, T(name), args);
}
