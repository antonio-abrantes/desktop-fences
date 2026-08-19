namespace DesktopFences.Core.Fences;

/// <summary>
/// Identifica o ficheiro vazio que o Explorer cria via ShellNew (.desktopfence).
/// Não é um ícone do utilizador: nunca vai para o store.
/// </summary>
public static class DesktopFenceStubRules
{
    public const string Extension = ".desktopfence";

    public static bool HasStubExtension(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string trimmed = path.Trim().Trim('"');
        return string.Equals(Path.GetExtension(trimmed), Extension, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ForbidsCustody(string? name, string? path = null, string? originalPath = null) =>
        HasStubExtension(name) || HasStubExtension(path) || HasStubExtension(originalPath);

    public static bool IsStubPath(string? path, IEnumerable<string> desktopRoots)
    {
        if (string.IsNullOrWhiteSpace(path) || desktopRoots is null)
            return false;

        if (!HasStubExtension(path))
            return false;

        string full;
        try
        {
            full = Path.GetFullPath(path.Trim().Trim('"'));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (Directory.Exists(full))
            return false;

        foreach (string root in desktopRoots)
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;

            string rootFull;
            try
            {
                rootFull = Path.GetFullPath(root);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            if (IsUnderDesktopRoot(full, rootFull))
                return true;
        }

        return false;
    }

    private static bool IsUnderDesktopRoot(string filePath, string desktopRoot)
    {
        string rootPrefix = Path.TrimEndingDirectorySeparator(desktopRoot)
                            + Path.DirectorySeparatorChar;
        return filePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
