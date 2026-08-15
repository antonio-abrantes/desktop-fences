namespace DesktopFences.Core.Fences;

public readonly record struct SnapRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

/// <summary>
/// Ímã ao soltar: bordas da área de trabalho e arestas de outras fences.
/// Não empurra vizinhos nem empilha. A folga é o limiar de captura, não uma margem extra.
/// </summary>
public static class FenceSnap
{
    public const double DefaultThreshold = 12;

    public static SnapRect Translate(
        SnapRect moving,
        SnapRect workArea,
        IReadOnlyList<SnapRect> others,
        double threshold = DefaultThreshold)
    {
        ArgumentNullException.ThrowIfNull(others);
        EnsureThreshold(threshold);

        double dx = Closest(XDeltas(moving, workArea, others), threshold) ?? 0;
        double dy = Closest(YDeltas(moving, workArea, others), threshold) ?? 0;
        return moving with { X = moving.X + dx, Y = moving.Y + dy };
    }

    public static SnapRect Edges(
        SnapRect moving,
        SnapRect workArea,
        IReadOnlyList<SnapRect> others,
        double minWidth,
        double minHeight,
        double threshold = DefaultThreshold)
    {
        ArgumentNullException.ThrowIfNull(others);
        EnsureThreshold(threshold);
        minWidth = Math.Max(1, minWidth);
        minHeight = Math.Max(1, minHeight);

        double left = SnapCoordinate(moving.X, LeftTargets(workArea, others), threshold);
        double right = SnapCoordinate(moving.Right, RightTargets(workArea, others), threshold);
        (left, right) = ResolvePair(moving.X, moving.Right, left, right, minWidth);

        double top = SnapCoordinate(moving.Y, TopTargets(workArea, others), threshold);
        double bottom = SnapCoordinate(moving.Bottom, BottomTargets(workArea, others), threshold);
        (top, bottom) = ResolvePair(moving.Y, moving.Bottom, top, bottom, minHeight);

        return new SnapRect(left, top, right - left, bottom - top);
    }

    private static void EnsureThreshold(double threshold)
    {
        if (threshold < 0)
            throw new ArgumentOutOfRangeException(nameof(threshold));
    }

    private static double SnapCoordinate(double current, IEnumerable<double> targets, double threshold)
    {
        double? delta = Closest(targets.Select(target => target - current), threshold);
        return current + (delta ?? 0);
    }

    private static (double Start, double End) ResolvePair(
        double originalStart,
        double originalEnd,
        double start,
        double end,
        double minSize)
    {
        if (end - start >= minSize)
            return (start, end);

        double dStart = Math.Abs(start - originalStart);
        double dEnd = Math.Abs(end - originalEnd);
        if (dStart <= dEnd)
            end = originalEnd;
        else
            start = originalStart;

        if (end - start < minSize)
            return (originalStart, originalEnd);

        return (start, end);
    }

    private static IEnumerable<double> XDeltas(SnapRect moving, SnapRect work, IReadOnlyList<SnapRect> others)
    {
        foreach (double target in LeftTargets(work, others))
            yield return target - moving.X;
        foreach (double target in RightTargets(work, others))
            yield return target - moving.Right;
    }

    private static IEnumerable<double> YDeltas(SnapRect moving, SnapRect work, IReadOnlyList<SnapRect> others)
    {
        foreach (double target in TopTargets(work, others))
            yield return target - moving.Y;
        foreach (double target in BottomTargets(work, others))
            yield return target - moving.Bottom;
    }

    private static IEnumerable<double> LeftTargets(SnapRect work, IReadOnlyList<SnapRect> others)
    {
        if (HasArea(work))
            yield return work.X;
        foreach (SnapRect other in others)
        {
            if (!HasArea(other))
                continue;
            yield return other.X;
            yield return other.Right;
        }
    }

    private static IEnumerable<double> RightTargets(SnapRect work, IReadOnlyList<SnapRect> others)
    {
        if (HasArea(work))
            yield return work.Right;
        foreach (SnapRect other in others)
        {
            if (!HasArea(other))
                continue;
            yield return other.Right;
            yield return other.X;
        }
    }

    private static IEnumerable<double> TopTargets(SnapRect work, IReadOnlyList<SnapRect> others)
    {
        if (HasArea(work))
            yield return work.Y;
        foreach (SnapRect other in others)
        {
            if (!HasArea(other))
                continue;
            yield return other.Y;
            yield return other.Bottom;
        }
    }

    private static IEnumerable<double> BottomTargets(SnapRect work, IReadOnlyList<SnapRect> others)
    {
        if (HasArea(work))
            yield return work.Bottom;
        foreach (SnapRect other in others)
        {
            if (!HasArea(other))
                continue;
            yield return other.Bottom;
            yield return other.Y;
        }
    }

    private static bool HasArea(SnapRect rect) => rect.Width > 0 && rect.Height > 0;

    private static double? Closest(IEnumerable<double> deltas, double threshold)
    {
        double? best = null;
        double bestAbs = double.PositiveInfinity;
        foreach (double delta in deltas)
        {
            double abs = Math.Abs(delta);
            if (abs <= threshold && abs < bestAbs)
            {
                best = delta;
                bestAbs = abs;
            }
        }

        return best;
    }
}
