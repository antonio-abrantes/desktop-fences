namespace DesktopFences.Core.Occupancy;

public enum FenceItemDropKind
{
    Stay,
    Transfer,
    Eject
}

/// <summary>
/// Retângulos de ecrã em pixels. Last-in-list is treated as top-most when two fences overlap.
/// </summary>
public readonly record struct FenceScreenTarget(
    Guid Id,
    int WindowX,
    int WindowY,
    int WindowWidth,
    int WindowHeight,
    int BodyX,
    int BodyY,
    int BodyWidth,
    int BodyHeight,
    bool Collapsed)
{
    public bool ContainsWindow(int x, int y) =>
        WindowWidth > 0
        && WindowHeight > 0
        && x >= WindowX
        && x < WindowX + WindowWidth
        && y >= WindowY
        && y < WindowY + WindowHeight;

    public bool ContainsBody(int x, int y) =>
        !Collapsed
        && BodyWidth > 0
        && BodyHeight > 0
        && x >= BodyX
        && x < BodyX + BodyWidth
        && y >= BodyY
        && y < BodyY + BodyHeight;
}

/// <summary>
/// No soltar de um item: corpo de outra fence transfere; chrome (incl. recolhida) não ejetar;
/// desktop ejetar. O hide do ícone real não muda de sítio — só o dono no JSON.
/// </summary>
public readonly record struct FenceItemDropResult(FenceItemDropKind Kind, Guid? TargetId);

public static class FenceItemDrop
{
    public static FenceItemDropResult Evaluate(
        IReadOnlyList<FenceScreenTarget> fences,
        Guid sourceId,
        int x,
        int y)
    {
        ArgumentNullException.ThrowIfNull(fences);

        FenceScreenTarget? body = LastMatch(fences, t => t.ContainsBody(x, y));
        if (body is { } hitBody)
        {
            return hitBody.Id == sourceId
                ? new FenceItemDropResult(FenceItemDropKind.Stay, null)
                : new FenceItemDropResult(FenceItemDropKind.Transfer, hitBody.Id);
        }

        if (LastMatch(fences, t => t.ContainsWindow(x, y)) is not null)
            return new FenceItemDropResult(FenceItemDropKind.Stay, null);

        return new FenceItemDropResult(FenceItemDropKind.Eject, null);
    }

    public static FenceItemDropKind Resolve(
        IReadOnlyList<FenceScreenTarget> fences,
        Guid sourceId,
        int x,
        int y) =>
        Evaluate(fences, sourceId, x, y).Kind;

    public static Guid? TransferTargetId(
        IReadOnlyList<FenceScreenTarget> fences,
        Guid sourceId,
        int x,
        int y) =>
        Evaluate(fences, sourceId, x, y).TargetId;

    private static FenceScreenTarget? LastMatch(
        IReadOnlyList<FenceScreenTarget> fences,
        Func<FenceScreenTarget, bool> predicate)
    {
        for (int i = fences.Count - 1; i >= 0; i--)
        {
            if (predicate(fences[i]))
                return fences[i];
        }

        return null;
    }
}
