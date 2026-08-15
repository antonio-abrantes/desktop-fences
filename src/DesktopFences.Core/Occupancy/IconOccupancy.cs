using DesktopFences.Core.Models;

namespace DesktopFences.Core.Occupancy;

/// <summary>
/// Decide quais ícones do desktop caem dentro de uma fence.
/// Célula padrão aproxima o hit-box do ícone+label do Explorer no Windows 11 (~80×112).
/// </summary>
public static class IconOccupancy
{
    public const int DefaultCellWidth = 80;
    public const int DefaultCellHeight = 112;
    public const int HiddenAwayThreshold = -10_000;

    public static IReadOnlyList<DesktopIcon> Inside(
        IEnumerable<DesktopIcon> icons,
        FenceBounds bounds,
        int cellWidth = DefaultCellWidth,
        int cellHeight = DefaultCellHeight)
    {
        ArgumentNullException.ThrowIfNull(icons);
        if (cellWidth <= 0) throw new ArgumentOutOfRangeException(nameof(cellWidth));
        if (cellHeight <= 0) throw new ArgumentOutOfRangeException(nameof(cellHeight));

        return icons
            .Where(IsPlacedOnDesktop)
            .Where(icon => bounds.Intersects(icon.X, icon.Y, cellWidth, cellHeight))
            .ToList();
    }

    public static DesktopIcon? Hit(
        IEnumerable<DesktopIcon> icons,
        int localX,
        int localY,
        int cellWidth = DefaultCellWidth,
        int cellHeight = DefaultCellHeight)
    {
        ArgumentNullException.ThrowIfNull(icons);
        return icons.FirstOrDefault(icon =>
            IsPlacedOnDesktop(icon)
            && localX >= icon.X && localX < icon.X + cellWidth
            && localY >= icon.Y && localY < icon.Y + cellHeight);
    }

    public static DesktopIcon? HitOrNearest(
        IEnumerable<DesktopIcon> icons,
        int localX,
        int localY,
        int cellWidth = DefaultCellWidth,
        int cellHeight = DefaultCellHeight,
        int maxDistance = 120)
    {
        DesktopIcon? direct = Hit(icons, localX, localY, cellWidth, cellHeight);
        if (direct is not null)
            return direct;

        DesktopIcon? best = null;
        int bestDist = maxDistance * maxDistance;
        foreach (DesktopIcon icon in icons)
        {
            if (!IsPlacedOnDesktop(icon))
                continue;
            int cx = icon.X + (cellWidth / 2);
            int cy = icon.Y + (cellHeight / 2);
            int dx = localX - cx;
            int dy = localY - cy;
            int dist = (dx * dx) + (dy * dy);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = icon;
            }
        }

        return best;
    }

    public static bool IsPlacedOnDesktop(DesktopIcon icon) =>
        icon.X > HiddenAwayThreshold && icon.Y > HiddenAwayThreshold;
}
