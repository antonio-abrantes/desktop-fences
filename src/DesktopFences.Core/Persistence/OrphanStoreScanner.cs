using DesktopFences.Core.Models;
using DesktopFences.Core.Transactions;

namespace DesktopFences.Core.Persistence;

public static class OrphanStoreScanner
{
    public static IReadOnlyList<string> Find(
        string itemsRoot,
        LayoutDocument layout,
        IEnumerable<CustodyTransaction> transactions)
    {
        if (!Directory.Exists(itemsRoot))
            return [];
        var referenced = layout.Fences.SelectMany(f => f.Items).Select(i => i.ItemId)
            .Concat(transactions.SelectMany(t => t.Items).Select(i => i.ItemId))
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        return Directory.EnumerateDirectories(itemsRoot)
            .Where(path => Guid.TryParse(Path.GetFileName(path), out Guid id) && !referenced.Contains(id))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
