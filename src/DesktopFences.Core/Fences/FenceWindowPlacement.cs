namespace DesktopFences.Core.Fences;

public static class FenceWindowPlacement
{
    public static (double X, double Y) ClampToWorkArea(
        double x,
        double y,
        double width,
        double height,
        double workX,
        double workY,
        double workWidth,
        double workHeight)
    {
        double maxX = workX + workWidth - width;
        double maxY = workY + workHeight - height;
        if (maxX < workX)
            maxX = workX;
        if (maxY < workY)
            maxY = workY;

        return (Math.Clamp(x, workX, maxX), Math.Clamp(y, workY, maxY));
    }
}
