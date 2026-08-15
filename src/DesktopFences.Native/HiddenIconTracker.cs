using DesktopFences.Core.Models;

namespace DesktopFences.Native;

/// <summary>
/// Guarda posições originais para restore obrigatório no shutdown.
/// </summary>
public sealed class HiddenIconTracker
{
    private readonly Dictionary<int, DesktopIcon> _original = [];

    public int Count => _original.Count;

    public IReadOnlyList<DesktopIcon> Icons => _original.Values.ToList();

    public void Remember(DesktopIcon icon)
    {
        _original.TryAdd(icon.Index, icon);
    }

    public IReadOnlyList<(int Index, int X, int Y)> Snapshot() =>
        _original.Select(kv => (kv.Key, kv.Value.X, kv.Value.Y)).ToList();

    public void Clear() => _original.Clear();

    public DesktopIcon? ReleaseByName(string name)
    {
        string file = Path.GetFileName(name);
        string stem = Path.GetFileNameWithoutExtension(file);
        KeyValuePair<int, DesktopIcon> match = _original
            .FirstOrDefault(kv =>
                kv.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                || kv.Value.Name.Equals(file, StringComparison.OrdinalIgnoreCase)
                || kv.Value.Name.Equals(stem, StringComparison.OrdinalIgnoreCase)
                || Path.GetFileNameWithoutExtension(kv.Value.Name)
                    .Equals(stem, StringComparison.OrdinalIgnoreCase));
        if (match.Value is null)
            return null;

        _original.Remove(match.Key);
        return match.Value;
    }
}
