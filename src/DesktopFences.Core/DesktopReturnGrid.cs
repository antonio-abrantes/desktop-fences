using DesktopFences.Core.Models;

namespace DesktopFences.Core;

public static class DesktopReturnGrid
{
    public static IReadOnlyDictionary<string, (int X, int Y)> PreserveExisting(
        IReadOnlyDictionary<string, (int X, int Y)> saved,
        IReadOnlyDictionary<string, (int X, int Y)> verified,
        IReadOnlyList<(int X, int Y)> otherOccupants,
        int width,
        int height)
    {
        var corrections = new Dictionary<string, (int X, int Y)>(StringComparer.OrdinalIgnoreCase);
        var stationary = new List<(int X, int Y)>(otherOccupants);
        foreach (var (id, current) in verified)
        {
            if (saved.TryGetValue(id, out var original) && original != current)
                corrections.Add(id, original);
            else
                stationary.Add(current);
        }

        // If an unrelated icon took a slot, leave it alone and keep that slot blocked.
        // Removing a move can block another move that depended on it vacating a slot.
        while (true)
        {
            var blocked = corrections.Where(move => stationary.Any(point =>
                Math.Abs(move.Value.X - point.X) < width && Math.Abs(move.Value.Y - point.Y) < height)).ToArray();
            if (blocked.Length == 0)
                return corrections;
            foreach (var move in blocked)
            {
                corrections.Remove(move.Key);
                stationary.Add(verified[move.Key]);
            }
        }
    }

    public static IReadOnlyDictionary<string, (int X, int Y)> ReserveAfterReturn(
        IReadOnlyList<((int X, int Y) Point, string Id, bool PreferExact)> requests,
        IReadOnlyList<(int X, int Y)> protectedPositions,
        IReadOnlyList<(int X, int Y)> liveExternalPositions,
        IReadOnlyList<(PixelRect Bounds, (int X, int Y) Origin)> areas,
        int width,
        int height) =>
        Reserve(requests, protectedPositions.Concat(liveExternalPositions).Distinct().ToArray(), areas, width, height);

    public static IReadOnlyDictionary<string, (int X, int Y)> Reserve(
        IReadOnlyList<((int X, int Y) Point, string Id, bool PreferExact)> requests,
        IReadOnlyList<(int X, int Y)> occupied,
        IReadOnlyList<(PixelRect Bounds, (int X, int Y) Origin)> areas,
        int width,
        int height)
    {
        var reservations = new Dictionary<string, (int X, int Y)>(StringComparer.OrdinalIgnoreCase);
        if (width <= 0 || height <= 0)
            return reservations;

        var grid = new List<(int X, int Y)>();
        foreach ((PixelRect bounds, (int X, int Y) origin) in areas)
        {
            int firstX = origin.X + (int)Math.Ceiling((double)(bounds.X - origin.X) / width) * width;
            int firstY = origin.Y + (int)Math.Ceiling((double)(bounds.Y - origin.Y) / height) * height;
            for (int y = firstY; y <= bounds.Y + bounds.Height - height; y += height)
            for (int x = firstX; x <= bounds.X + bounds.Width - width; x += width)
                grid.Add((x, y));
        }

        var taken = new List<(int X, int Y)>(occupied);
        bool IsFree((int X, int Y) point) =>
            !taken.Any(other => Math.Abs(point.X - other.X) < width && Math.Abs(point.Y - other.Y) < height);

        // Give unobstructed original positions priority over a neighbour's fallback.
        foreach (var request in requests)
        {
            var point = request.Point;
            if (request.PreferExact && !reservations.ContainsKey(request.Id) &&
                areas.Any(area => area.Bounds.Contains(point.X, point.Y)) && IsFree(point))
            {
                reservations.Add(request.Id, point);
                taken.Add(point);
            }
        }

        var free = grid.Distinct().Where(IsFree).ToList();
        foreach (var request in requests)
        {
            if (reservations.ContainsKey(request.Id) || free.Count == 0)
                continue;
            var point = free.MinBy(cell =>
            {
                long dx = (long)cell.X - request.Point.X;
                long dy = (long)cell.Y - request.Point.Y;
                return dx * dx + dy * dy;
            });
            reservations.Add(request.Id, point);
            free.RemoveAll(cell => Math.Abs(cell.X - point.X) < width && Math.Abs(cell.Y - point.Y) < height);
        }
        return reservations;
    }
}
