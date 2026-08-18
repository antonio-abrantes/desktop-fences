namespace DesktopFences.Core.Fences;

public static class DesktopPlacementRetryRules
{
    public const int MaxAttempts = 8;
    public const int MinAttemptsBeforeSuccess = 2;

    public static bool IsComplete(int attempts, int positioned, int expectedCount) =>
        attempts >= MaxAttempts
        || (attempts >= MinAttemptsBeforeSuccess && positioned >= expectedCount);
}
