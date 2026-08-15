using DesktopFences.Core.Models;

namespace DesktopFences.Core;

public static class DesktopIconMatcher
{
    public static DesktopIcon? Find(IEnumerable<DesktopIcon> icons, string pathOrName)
    {
        ArgumentNullException.ThrowIfNull(icons);
        if (string.IsNullOrWhiteSpace(pathOrName))
            return null;

        string fileName = Path.GetFileName(pathOrName);
        string stem = Path.GetFileNameWithoutExtension(fileName);

        return icons.FirstOrDefault(icon =>
            icon.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase) ||
            icon.Name.Equals(stem, StringComparison.OrdinalIgnoreCase));
    }
}
