namespace DesktopFences.Core;

/// <summary>
/// Pasta gerida pelo app: o Explorer não desenha o que não está no Desktop.
/// </summary>
public static class FenceItemStore
{
    public static string Root() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopFences",
            "Items");

    public static string FolderForItem(Guid itemId) => FolderForItem(Root(), itemId);

    public static string FolderForItem(string root, Guid itemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        if (itemId == Guid.Empty)
            throw new ArgumentException("ItemId não pode ser vazio.", nameof(itemId));
        return Path.Combine(root, itemId.ToString("D"));
    }

    public static string PayloadPath(Guid itemId, string storageName) =>
        PayloadPath(Root(), itemId, storageName);

    public static string PayloadPath(string root, Guid itemId, string storageName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageName);
        if (!string.Equals(Path.GetFileName(storageName), storageName, StringComparison.Ordinal)
            || storageName is "." or "..")
            throw new ArgumentException("StorageName deve ser apenas um nome relativo.", nameof(storageName));
        return Path.Combine(FolderForItem(root, itemId), storageName);
    }

    public static string LegacyFolderForFence(Guid fenceId) =>
        Path.Combine(Root(), fenceId.ToString("D"));

    public static bool IsUnderRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string root = Root().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                      + Path.DirectorySeparatorChar;
        return path.Trim().StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    public static string UniqueDestination(string folder, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        string safe = string.IsNullOrWhiteSpace(fileName) ? "item" : fileName.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
            safe = safe.Replace(invalid, '_');

        string dest = Path.Combine(folder, safe);
        if (!Exists(dest))
            return dest;

        string stem = Path.GetFileNameWithoutExtension(safe);
        string ext = Path.GetExtension(safe);
        for (int n = 2; n < 10_000; n++)
        {
            dest = Path.Combine(folder, $"{stem} ({n}){ext}");
            if (!Exists(dest))
                return dest;
        }

        return Path.Combine(folder, $"{stem}-{Guid.NewGuid():N}{ext}");
    }

    public static string RestoreDirectory(string? originalPath, IReadOnlyList<string> desktopFolders)
    {
        string? userDesktop = desktopFolders.FirstOrDefault(folder =>
            !string.IsNullOrWhiteSpace(folder));
        if (string.IsNullOrWhiteSpace(userDesktop) || !Directory.Exists(userDesktop))
            userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        if (!string.IsNullOrWhiteSpace(originalPath) && !string.IsNullOrWhiteSpace(userDesktop))
        {
            string? dir = Path.GetDirectoryName(originalPath.Trim());
            // FolderList põe o Desktop do utilizador antes do Desktop Público.
            // O processo asInvoker não pode assumir escrita no Desktop Público;
            // restaurar lá tornaria o item um bloqueador permanente do lote.
            if (!string.IsNullOrEmpty(dir)
                && Directory.Exists(dir)
                && SamePath(dir, userDesktop))
                return dir;
        }

        return userDesktop ?? string.Empty;
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}
