using System.Text.Json.Serialization;

namespace DesktopFences.Core.Transactions;

public enum CustodyOperationKind
{
    Inbound,
    Outbound,
    Pause,
    Resume,
    RemoveFence,
    Shutdown,
    Migration
}

public enum CustodyTransactionState
{
    Prepared,
    PayloadChanged,
    LayoutCommitted,
    Completed,
    FailedRecoverable
}

public sealed class CustodyTransaction
{
    public Guid OperationId { get; set; } = Guid.NewGuid();
    public CustodyOperationKind Operation { get; set; }
    public CustodyTransactionState State { get; set; } = CustodyTransactionState.Prepared;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public long LayoutRevisionBefore { get; set; }
    public long LayoutRevisionAfter { get; set; }
    public List<CustodyTransactionItem> Items { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }
}

public sealed class CustodyTransactionItem
{
    public Guid ItemId { get; set; }
    public Guid? SourceFenceId { get; set; }
    public Guid? TargetFenceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SourcePath { get; set; }
    public string? DestinationPath { get; set; }
    public bool NamespaceItem { get; set; }
    public string? NamespaceKey { get; set; }
    public bool DestinationNamespaceHidden { get; set; }
}
