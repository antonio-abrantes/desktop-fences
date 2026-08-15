using DesktopFences.Core.Models;

namespace DesktopFences.Core.Fences;

public static class FenceLayoutRules
{
    public const double DefaultWidth = 380;
    public const double DefaultHeight = 280;
    public const double DefaultX = 80;
    public const double DefaultY = 80;
    public const double PlaceOffset = 40;

    public static FenceState CreateDefault() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Nova fence",
        TitleAlignment = TitleAlignment.Left,
        X = DefaultX,
        Y = DefaultY,
        Width = DefaultWidth,
        Height = DefaultHeight
    };

    public static void EnsureAtLeastOne(List<FenceState> fences)
    {
        ArgumentNullException.ThrowIfNull(fences);
        if (fences.Count == 0)
            fences.Add(CreateDefault());
    }

    public static bool CanRemove(int count) => count > 1;

    public static FenceState PlaceNew(IReadOnlyList<FenceState> existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        FenceState last = existing.Count > 0 ? existing[^1] : CreateDefault();
        return new FenceState
        {
            Id = Guid.NewGuid(),
            Title = "Nova fence",
            TitleAlignment = TitleAlignment.Left,
            X = last.X + PlaceOffset,
            Y = last.Y + PlaceOffset,
            Width = last.Width > 0 ? last.Width : DefaultWidth,
            Height = last.Height > 0 ? last.Height : DefaultHeight
        };
    }
}
