namespace DesktopFences.Core.Fences;

public static class UiLanguageCodes
{
    public const string System = "system";
    public const string Portuguese = "pt";
    public const string English = "en";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return System;

        string code = value.Trim().ToLowerInvariant();
        return code is Portuguese or English or System ? code : System;
    }
}
