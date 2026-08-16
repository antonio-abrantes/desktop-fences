namespace DesktopFences.Native;

public readonly record struct DesktopConcealResult(
    bool Applied,
    string? StoragePath,
    string? OriginalPath);
