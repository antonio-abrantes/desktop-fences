namespace DesktopFences.Core.Fences;

/// <summary>
/// O Explorer não enumera ShellNew em direto: usa a cache
/// HKCU\...\Explorer\Discardable\PostSetup\ShellNew\Classes.
/// Sem esta entrada o item Novo → Fence não aparece, nem no menu clássico.
/// </summary>
public static class ShellNewMenuCache
{
    public static IReadOnlyList<string> WithExtension(IReadOnlyList<string> existing, string extension)
    {
        ArgumentNullException.ThrowIfNull(existing);
        string normalized = Normalize(extension);
        if (normalized.Length == 0)
            return existing;

        foreach (string item in existing)
        {
            if (string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase))
                return existing;
        }

        var next = new List<string>(existing.Count + 1);
        next.AddRange(existing);
        next.Add(normalized);
        return next;
    }

    public static IReadOnlyList<string> WithoutExtension(IReadOnlyList<string> existing, string extension)
    {
        ArgumentNullException.ThrowIfNull(existing);
        string normalized = Normalize(extension);
        if (normalized.Length == 0)
            return existing;

        var next = new List<string>(existing.Count);
        foreach (string item in existing)
        {
            if (!string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase))
                next.Add(item);
        }

        return next.Count == existing.Count ? existing : next;
    }

    private static string Normalize(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return "";

        string trimmed = extension.Trim();
        if (trimmed.Length == 0)
            return "";

        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }
}
