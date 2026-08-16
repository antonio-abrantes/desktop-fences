using DesktopFences.Core.Models;

namespace DesktopFences.Core.Recovery;

public sealed class DesktopRecoverySnapshot
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public DateTimeOffset CapturedUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<DesktopRecoveryItem> Items { get; set; } = [];
}

public sealed class DesktopRecoveryItem
{
    public Guid? ItemId { get; set; }
    public FenceItemKind Kind { get; set; } = FenceItemKind.Stored;
    public string Name { get; set; } = string.Empty;
    public string? OriginalPath { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

public static class DesktopRecoverySnapshotBuilder
{
    public static DesktopRecoverySnapshot Build(
        IReadOnlyList<DesktopIcon> visibleIcons,
        LayoutDocument layout,
        Func<string, string?> resolvePath,
        DesktopRecoverySnapshot? previous = null)
    {
        ArgumentNullException.ThrowIfNull(visibleIcons);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(resolvePath);

        var byKey = new Dictionary<string, DesktopRecoveryItem>(StringComparer.OrdinalIgnoreCase);
        if (previous is not null)
        {
            foreach (DesktopRecoveryItem item in previous.Items.Where(IsValid))
                byKey[Key(item.OriginalPath, item.Name)] = Clone(item);
        }

        foreach (FenceItemState item in layout.Fences.SelectMany(fence => fence.Items))
        {
            if (item.OriginalX is not int x || item.OriginalY is not int y)
                continue;
            var recovery = new DesktopRecoveryItem
            {
                ItemId = item.ItemId == Guid.Empty ? null : item.ItemId,
                Kind = item.Kind,
                Name = item.Name,
                OriginalPath = item.OriginalPath,
                X = x,
                Y = y
            };
            byKey[Key(recovery.OriginalPath, recovery.Name)] = recovery;
        }

        // O Explorer é a fonte mais recente para itens atualmente visíveis.
        foreach (DesktopIcon icon in visibleIcons)
        {
            string? path = resolvePath(icon.Name);
            DesktopRecoveryItem? byName = path is null
                ? byKey.Values.FirstOrDefault(item => item.Name.Equals(icon.Name, StringComparison.OrdinalIgnoreCase))
                : null;
            path ??= byName?.OriginalPath;
            string key = Key(path, icon.Name);
            byKey[key] = new DesktopRecoveryItem
            {
                ItemId = byKey.TryGetValue(key, out DesktopRecoveryItem? known) ? known.ItemId : byName?.ItemId,
                Kind = known?.Kind ?? byName?.Kind ?? FenceItemKind.Stored,
                Name = icon.Name,
                OriginalPath = path ?? known?.OriginalPath ?? byName?.OriginalPath,
                X = icon.X,
                Y = icon.Y
            };
        }

        return new DesktopRecoverySnapshot
        {
            CapturedUtc = DateTimeOffset.UtcNow,
            Items = byKey.Values.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static bool IsValid(DesktopRecoveryItem item) =>
        !string.IsNullOrWhiteSpace(item.Name);

    private static DesktopRecoveryItem Clone(DesktopRecoveryItem item) => new()
    {
        ItemId = item.ItemId,
        Kind = item.Kind,
        Name = item.Name,
        OriginalPath = item.OriginalPath,
        X = item.X,
        Y = item.Y
    };

    private static string Key(string? path, string name)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            try { return "P|" + Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar); }
            catch { return "P|" + path.Trim(); }
        }
        return "N|" + name.Trim();
    }
}
