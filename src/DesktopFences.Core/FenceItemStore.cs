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

    public static string FolderFor(Guid fenceId) =>
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
        if (!string.IsNullOrWhiteSpace(originalPath))
        {
            string? dir = Path.GetDirectoryName(originalPath.Trim());
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && !IsUnderRoot(dir))
                return dir;
        }

        foreach (string folder in desktopFolders)
        {
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                return folder;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}
