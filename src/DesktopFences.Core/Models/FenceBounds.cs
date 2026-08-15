namespace DesktopFences.Core.Models;

/// <summary>
/// Retângulo da fence no mesmo espaço de coordenadas dos ícones (pixels de tela/ListView).
/// </summary>
public readonly record struct FenceBounds(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public bool Intersects(double left, double top, double width, double height)
    {
        return left < Right
            && left + width > X
            && top < Bottom
            && top + height > Y;
    }
}
