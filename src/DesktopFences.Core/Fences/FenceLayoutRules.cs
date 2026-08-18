using DesktopFences.Core.Models;

namespace DesktopFences.Core.Fences;

public static class FenceLayoutRules
{
    public const double DefaultWidth = 380;
    public const double DefaultHeight = 280;
    public const double DefaultX = 80;
    public const double DefaultY = 80;
    public const double PlaceOffset = 40;
    public const string DefaultTitle = "Nova fence";

    public static FenceState CreateDefault(
        string? title = null,
        TitleAlignment? defaultAlignment = null,
        FenceTheme? defaultTheme = null)
    {
        var state = new FenceState
        {
            Id = Guid.NewGuid(),
            Title = ResolveTitle(title),
            X = DefaultX,
            Y = DefaultY,
            Width = DefaultWidth,
            Height = DefaultHeight
        };
        ApplyAppearanceDefaults(state, defaultAlignment, defaultTheme);
        return state;
    }

    public static void EnsureAtLeastOne(
        List<FenceState> fences,
        string? title = null,
        TitleAlignment? defaultAlignment = null,
        FenceTheme? defaultTheme = null)
    {
        ArgumentNullException.ThrowIfNull(fences);
        if (fences.Count == 0)
            fences.Add(CreateDefault(title, defaultAlignment, defaultTheme));
    }

    public static bool CanRemove(int count) => count > 1;

    public static FenceState PlaceNew(
        IReadOnlyList<FenceState> existing,
        string? title = null,
        TitleAlignment? defaultAlignment = null,
        FenceTheme? defaultTheme = null)
    {
        ArgumentNullException.ThrowIfNull(existing);
        FenceState last = existing.Count > 0
            ? existing[^1]
            : CreateDefault(title, defaultAlignment, defaultTheme);
        var state = new FenceState
        {
            Id = Guid.NewGuid(),
            Title = ResolveTitle(title),
            X = last.X + PlaceOffset,
            Y = last.Y + PlaceOffset,
            Width = last.Width > 0 ? last.Width : DefaultWidth,
            Height = last.Height > 0 ? last.Height : DefaultHeight
        };
        ApplyAppearanceDefaults(state, defaultAlignment, defaultTheme);
        return state;
    }

    private static void ApplyAppearanceDefaults(
        FenceState state,
        TitleAlignment? defaultAlignment,
        FenceTheme? defaultTheme)
    {
        state.TitleAlignment = defaultAlignment ?? TitleAlignment.Left;
        if (defaultTheme is null)
            return;

        FenceTheme normalized = defaultTheme.Normalized();
        state.Theme = normalized.IsDefault ? null : normalized;
    }

    private static string ResolveTitle(string? title) =>
        string.IsNullOrWhiteSpace(title) ? DefaultTitle : title.Trim();
}
