using DesktopFences.Core.Models;
using DesktopFences.Core.Persistence;

namespace DesktopFences.Core.Fences;

public static class FenceOwnership
{
    /// <summary>Transfere somente a propriedade no documento; não conhece nem toca no payload.</summary>
    public static LayoutDocument Transfer(
        LayoutDocument source,
        Guid sourceFenceId,
        Guid targetFenceId,
        IReadOnlyCollection<Guid> itemIds,
        int insertIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (sourceFenceId == targetFenceId)
            throw new ArgumentException("A origem e o destino devem ser diferentes.");
        LayoutDocument result = LayoutStore.Clone(source);
        FenceState from = result.Fences.SingleOrDefault(f => f.Id == sourceFenceId)
                          ?? throw new InvalidOperationException("Fence de origem não encontrada.");
        FenceState to = result.Fences.SingleOrDefault(f => f.Id == targetFenceId)
                        ?? throw new InvalidOperationException("Fence de destino não encontrada.");
        HashSet<Guid> ids = itemIds.ToHashSet();
        List<FenceItemState> moving = from.Items.Where(i => ids.Contains(i.ItemId)).ToList();
        if (moving.Count != ids.Count)
            throw new InvalidOperationException("Um ou mais itens não pertencem à fence de origem.");
        if (to.Items.Any(i => ids.Contains(i.ItemId)))
            throw new InvalidOperationException("O destino já contém um dos ItemIds.");
        from.Items.RemoveAll(i => ids.Contains(i.ItemId));
        to.Items.InsertRange(Math.Clamp(insertIndex, 0, to.Items.Count), moving);
        return result;
    }
}
