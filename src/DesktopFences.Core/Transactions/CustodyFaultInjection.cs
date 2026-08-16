namespace DesktopFences.Core.Transactions;

public enum CustodyCheckpoint
{
    Prepared,
    PayloadExecuted,
    PayloadChanged,
    LayoutSaved,
    LayoutCommitted,
    UiApplied,
    Completed
}

public interface ICustodyFaultInjector
{
    void Hit(CustodyCheckpoint checkpoint, CustodyTransaction transaction);
}

public sealed class NoCustodyFaults : ICustodyFaultInjector
{
    public static NoCustodyFaults Instance { get; } = new();

    private NoCustodyFaults()
    {
    }

    public void Hit(CustodyCheckpoint checkpoint, CustodyTransaction transaction)
    {
    }
}
