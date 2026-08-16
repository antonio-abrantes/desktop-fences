namespace DesktopFences.Core.Models;

/// <summary>
/// Retângulo em pixels inteiros (área de trabalho / monitor / ListView).
/// </summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool Contains(int x, int y) =>
        x >= X && x < Right && y >= Y && y < Bottom;

    public bool Intersects(int left, int top, int width, int height) =>
        left < Right
        && left + width > X
        && top < Bottom
        && top + height > Y;
}

/// <summary>
/// Monitor e a sua área útil (rcWork) no mesmo espaço que os ícones do ListView.
/// </summary>
public readonly record struct DisplaySurface(PixelRect Monitor, PixelRect Work);
