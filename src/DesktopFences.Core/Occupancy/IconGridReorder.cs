namespace DesktopFences.Core.Occupancy;

/// <summary>
/// Índice de inserção numa grade WrapPanel (esquerda → direita, cima → baixo).
/// </summary>
public static class IconGridReorder
{
    public static int InsertIndex(
        int count,
        double x,
        double y,
        double tileWidth,
        double tileHeight,
        double panelWidth)
    {
        if (count <= 0)
            return 0;
        if (tileWidth <= 0 || tileHeight <= 0 || panelWidth <= 0)
            return Math.Clamp(count, 0, count);

        int columns = Math.Max(1, (int)Math.Floor(panelWidth / tileWidth));
        int col = x <= 0 ? 0 : (int)Math.Floor(x / tileWidth);
        int row = y <= 0 ? 0 : (int)Math.Floor(y / tileHeight);
        if (col >= columns)
            col = columns;

        double localX = x - (Math.Min(col, columns - 1) * tileWidth);
        if (col < columns && localX > tileWidth / 2)
            col++;

        int index = (row * columns) + col;
        return Math.Clamp(index, 0, count);
    }

    public static void MoveBlock<T>(IList<T> items, IReadOnlyList<T> block, int insertIndex)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(block);
        if (block.Count == 0)
            return;

        int insert = Math.Clamp(insertIndex, 0, items.Count);
        foreach (T item in block)
        {
            int i = IndexOf(items, item);
            if (i >= 0 && i < insert)
                insert--;
        }

        foreach (T item in block)
        {
            int i = IndexOf(items, item);
            if (i >= 0)
                items.RemoveAt(i);
        }

        insert = Math.Clamp(insert, 0, items.Count);
        for (int k = 0; k < block.Count; k++)
            items.Insert(insert + k, block[k]);
    }

    private static int IndexOf<T>(IList<T> items, T item)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(items[i], item))
                return i;
        }

        return -1;
    }
}
