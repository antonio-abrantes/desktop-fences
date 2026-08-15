namespace DesktopFences.Core.Models;

/// <summary>
/// Ícone real do desktop, já traduzido para tipos gerenciados.
/// Coordenadas em pixels do SysListView32 (origem no canto do desktop daquela view).
/// </summary>
public sealed record DesktopIcon(int Index, string Name, int X, int Y);
