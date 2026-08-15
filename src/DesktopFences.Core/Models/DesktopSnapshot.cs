namespace DesktopFences.Core.Models;

public sealed record DesktopSnapshot(
    bool Connected,
    string HandleHex,
    IReadOnlyList<DesktopIcon> Icons,
    string? Error)
{
    public static DesktopSnapshot Failed(string error) =>
        new(false, "0x0", Array.Empty<DesktopIcon>(), error);
}
