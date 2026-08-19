using System.Globalization;
using System.Text;

namespace DesktopFences.Core.Install;

public sealed record MaintenanceResultRecord(
    DateTimeOffset Utc,
    string Mode,
    MaintenanceFailureKind Kind,
    int ExitCode,
    string Message)
{
    public string ToLogText()
    {
        var text = new StringBuilder();
        text.Append("utc=").AppendLine(Utc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
        text.Append("mode=").AppendLine(Sanitize(Mode));
        text.Append("kind=").AppendLine(Kind.ToString());
        text.Append("exitCode=").AppendLine(ExitCode.ToString(CultureInfo.InvariantCulture));
        text.Append("message=").AppendLine(Sanitize(Message));
        return text.ToString();
    }

    public static bool TryReadKind(string logText, out MaintenanceFailureKind kind)
    {
        kind = MaintenanceFailureKind.None;
        if (string.IsNullOrWhiteSpace(logText))
            return false;

        foreach (string raw in logText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!raw.StartsWith("kind=", StringComparison.OrdinalIgnoreCase))
                continue;
            string value = raw["kind=".Length..].Trim();
            return Enum.TryParse(value, ignoreCase: true, out kind);
        }

        return false;
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string flat = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return flat.Length <= 200 ? flat : flat[..200];
    }
}
