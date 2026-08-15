namespace DesktopFences.Core;

public static class DesktopPaths
{
    public static IEnumerable<string> Folders()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
    }

    /// <summary>
    /// Atalho .lnk / .url ganha de pasta com o mesmo nome visível
    /// (senão o ícone do atalho vira pasta).
    /// </summary>
    public static string? ResolveExisting(string nameOrPath)
    {
        if (string.IsNullOrWhiteSpace(nameOrPath))
            return null;

        string trimmed = nameOrPath.Trim().Trim('"');
        if (Path.IsPathRooted(trimmed) && Exists(trimmed))
        {
            if (File.Exists(trimmed))
                return trimmed;

            string? shortcut = SiblingShortcut(trimmed);
            return shortcut ?? trimmed;
        }

        string name = Path.GetFileName(trimmed);
        string stem = Path.GetFileNameWithoutExtension(name);
        if (string.IsNullOrEmpty(name))
            return Exists(trimmed) ? trimmed : null;

        string? file = null;
        foreach (string folder in Folders())
        {
            if (string.IsNullOrEmpty(folder))
                continue;

            foreach (string candidate in FileCandidates(folder, name, stem))
            {
                if (File.Exists(candidate))
                {
                    file = candidate;
                    break;
                }
            }

            if (file is not null)
                break;
        }

        if (file is not null)
            return file;

        string? directory = null;
        foreach (string folder in Folders())
        {
            if (string.IsNullOrEmpty(folder))
                continue;

            foreach (string candidate in DirectoryCandidates(folder, name, stem))
            {
                if (Directory.Exists(candidate))
                {
                    directory = candidate;
                    break;
                }
            }

            if (directory is not null)
                break;
        }

        return directory ?? (Exists(trimmed) ? trimmed : null);
    }

    private static IEnumerable<string> FileCandidates(string folder, string name, string stem)
    {
        yield return Path.Combine(folder, name);
        if (!HasExtension(name, ".lnk"))
            yield return Path.Combine(folder, name + ".lnk");
        if (!string.Equals(name, stem, StringComparison.OrdinalIgnoreCase) && !HasExtension(stem, ".lnk"))
            yield return Path.Combine(folder, stem + ".lnk");
        if (!HasExtension(name, ".url"))
            yield return Path.Combine(folder, stem + ".url");
    }

    private static IEnumerable<string> DirectoryCandidates(string folder, string name, string stem)
    {
        yield return Path.Combine(folder, name);
        if (!string.Equals(name, stem, StringComparison.OrdinalIgnoreCase))
            yield return Path.Combine(folder, stem);
    }

    private static string? SiblingShortcut(string directory)
    {
        string trimmed = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string? parent = Path.GetDirectoryName(trimmed);
        string name = Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            return null;

        string lnk = Path.Combine(parent, name + ".lnk");
        if (File.Exists(lnk))
            return lnk;

        string url = Path.Combine(parent, name + ".url");
        return File.Exists(url) ? url : null;
    }

    public static string VisibleName(string nameOrPath)
    {
        if (string.IsNullOrWhiteSpace(nameOrPath))
            return "";

        string name = Path.GetFileName(nameOrPath.Trim().Trim('"'));
        if (HasExtension(name, ".lnk") || HasExtension(name, ".url"))
            return Path.GetFileNameWithoutExtension(name);

        return name;
    }

    private static bool HasExtension(string name, string extension) =>
        name.EndsWith(extension, StringComparison.OrdinalIgnoreCase);

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}
