namespace DesktopFences.Core.Fences;

public static class ZOrderPlacement
{
    public static bool NeedsZOrderMove(long currentAbove, long desiredInsertAfter)
    {
        if (desiredInsertAfter == 0)
            return true;

        return currentAbove != desiredInsertAfter;
    }
}
