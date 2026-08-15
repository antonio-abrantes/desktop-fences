namespace DesktopFences.Core.Models;

public sealed class LayoutDocument
{
    public int Version { get; set; } = 1;
    public List<FenceState> Fences { get; set; } = [];
}

public sealed class FenceState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Fence";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 420;
    public double Height { get; set; } = 280;
    public string? MonitorDeviceName { get; set; }
    public bool Collapsed { get; set; }
    public List<FenceItemState> Items { get; set; } = [];
}

public sealed class FenceItemState
{
    public string Name { get; set; } = string.Empty;
    public string? Path { get; set; }
    public int? OriginalX { get; set; }
    public int? OriginalY { get; set; }
}
