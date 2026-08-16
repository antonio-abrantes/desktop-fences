using System.Reflection;

namespace DesktopFences.App;

internal static class AppInfo
{
    public const string ProductName = "DesktopFences";
    public const string Developer = "Antônio Abrantes";
    public const int Year = 2026;
    public const string ProfileUrl = "https://github.com/antonio-abrantes";
    public const string RepoUrl = "https://github.com/antonio-abrantes/desktop-fences";

    public static string VersionLabel
    {
        get
        {
            string? info = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            if (string.IsNullOrWhiteSpace(info))
            {
                Version? v = Assembly.GetExecutingAssembly().GetName().Version;
                info = v is null ? "0.6.0" : $"{v.Major}.{v.Minor}.{v.Build}";
            }

            int plus = info.IndexOf('+');
            if (plus >= 0)
                info = info[..plus];

            return info.StartsWith('v') ? info : "v" + info;
        }
    }
}
