namespace DesktopFences.Core.Transactions;

public sealed record CompensatingBatchResult<T>(
    bool Success,
    IReadOnlyList<T> Applied,
    bool CompensationComplete,
    T? FailedItem);

public static class CompensatingBatch
{
    public static CompensatingBatchResult<T> Execute<T>(
        IReadOnlyList<T> plans,
        Func<T, bool> apply,
        Func<T, bool> compensate)
    {
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(compensate);
        var applied = new List<T>();
        foreach (T plan in plans)
        {
            if (apply(plan))
            {
                applied.Add(plan);
                continue;
            }

            bool compensationComplete = true;
            foreach (T done in applied.AsEnumerable().Reverse())
                compensationComplete &= compensate(done);
            return new CompensatingBatchResult<T>(
                false, applied, compensationComplete, plan);
        }

        return new CompensatingBatchResult<T>(true, applied, true, default);
    }
}
