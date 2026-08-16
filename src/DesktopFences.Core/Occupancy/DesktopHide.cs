namespace DesktopFences.Core.Occupancy;

public enum DesktopHideKind
{
    None,
    MoveToStore,
    NamespaceIcon
}

public readonly record struct DesktopHidePlan(DesktopHideKind Kind, string Key);

/// <summary>
/// Decide como tirar um item do desktop sem coordenadas.
/// Ficheiro/atalho/pasta → mover para o store do Fence.
/// Lixeira / Este computador / Rede → CLSID (registry na Native).
/// </summary>
public static class DesktopHide
{
    public static readonly Guid RecycleBin = new("645FF040-5084-101B-9F08-00AA002F954E");
    public static readonly Guid ThisPc = new("20D04FE0-3AEA-1069-A2D8-08002B30309D");
    public static readonly Guid Network = new("F02C1A0D-BE21-4350-88B0-7367FC96EF3C");

    public static FileAttributes WithoutHidden(FileAttributes attributes) =>
        attributes & ~FileAttributes.Hidden;

    public static DesktopHidePlan For(
        string? pathOrName,
        IReadOnlyList<string> desktopFolders,
        string? parsingName = null)
    {
        ArgumentNullException.ThrowIfNull(desktopFolders);

        if (!string.IsNullOrWhiteSpace(pathOrName)
            && Path.IsPathRooted(pathOrName)
            && !TryNamespaceKey(pathOrName, out _))
        {
            return new DesktopHidePlan(DesktopHideKind.MoveToStore, pathOrName.Trim());
        }

        if (TryNamespaceKey(parsingName, out string nsKey)
            || TryNamespaceKey(pathOrName, out nsKey))
        {
            return new DesktopHidePlan(DesktopHideKind.NamespaceIcon, nsKey);
        }

        return new DesktopHidePlan(DesktopHideKind.None, pathOrName?.Trim() ?? "");
    }

    public static bool IsUnderDesktopFolders(string path, IReadOnlyList<string> desktopFolders)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(desktopFolders);

        string full = path.Trim();
        foreach (string folder in desktopFolders)
        {
            if (string.IsNullOrWhiteSpace(folder))
                continue;

            string prefix = folder.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool TryNamespaceKey(string? parsingName, out string key)
    {
        key = "";
        if (string.IsNullOrWhiteSpace(parsingName))
            return false;

        string trimmed = parsingName.Trim();
        if (TryShellAlias(trimmed, out Guid alias))
        {
            key = FormatClsid(alias);
            return true;
        }

        if (TryExtractClsid(trimmed, out Guid clsid))
        {
            key = FormatClsid(clsid);
            return true;
        }

        return false;
    }

    public static string FormatClsid(Guid clsid) => clsid.ToString("B").ToUpperInvariant();

    private static bool TryShellAlias(string value, out Guid clsid)
    {
        clsid = default;
        if (!value.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            return false;

        string rest = value["shell:".Length..];
        if (rest.StartsWith("::", StringComparison.Ordinal))
            return TryExtractClsid(rest, out clsid);

        if (rest.Equals("RecycleBinFolder", StringComparison.OrdinalIgnoreCase))
            return Assign(RecycleBin, out clsid);
        if (rest.Equals("MyComputerFolder", StringComparison.OrdinalIgnoreCase))
            return Assign(ThisPc, out clsid);
        if (rest.Equals("NetworkPlacesFolder", StringComparison.OrdinalIgnoreCase))
            return Assign(Network, out clsid);
        return false;
    }

    private static bool Assign(Guid value, out Guid clsid)
    {
        clsid = value;
        return true;
    }

    private static bool TryExtractClsid(string value, out Guid clsid)
    {
        clsid = default;
        int start = value.IndexOf('{');
        int end = value.IndexOf('}', start + 1);
        if (start < 0 || end <= start)
            return false;

        return Guid.TryParse(value[start..(end + 1)], out clsid);
    }
}
