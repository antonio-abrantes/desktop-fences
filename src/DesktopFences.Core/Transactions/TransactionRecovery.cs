using DesktopFences.Core.Models;

namespace DesktopFences.Core.Transactions;

public interface ICustodyRecoveryActions
{
    bool Exists(string path);
    bool Move(string source, string destination);
    bool SetNamespaceHidden(string key, bool hidden);
}

public sealed record RecoveryReport(int Recovered, int Pending, IReadOnlyList<string> Errors)
{
    public bool Complete => Pending == 0;
}

public sealed class TransactionRecovery
{
    private readonly ITransactionJournalStore _journals;
    private readonly ICustodyRecoveryActions _actions;

    public TransactionRecovery(ITransactionJournalStore journals, ICustodyRecoveryActions actions)
    {
        _journals = journals;
        _actions = actions;
    }

    public RecoveryReport Recover(LayoutDocument layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        int recovered = 0;
        int pending = 0;
        var errors = new List<string>();

        foreach (CustodyTransaction transaction in _journals.LoadAll())
        {
            bool rollForward = transaction.State is CustodyTransactionState.LayoutCommitted
                or CustodyTransactionState.Completed
                || (transaction.LayoutRevisionAfter > 0
                    && layout.Revision >= transaction.LayoutRevisionAfter);
            bool ok = true;
            foreach (CustodyTransactionItem item in transaction.Items)
            {
                bool destinationWanted = rollForward;
                if (!Reconcile(item, destinationWanted))
                {
                    ok = false;
                    errors.Add($"{transaction.OperationId:D}: não foi possível reconciliar {item.Name}.");
                }
            }

            if (ok)
            {
                _journals.Delete(transaction.OperationId);
                recovered++;
            }
            else
                pending++;
        }

        return new RecoveryReport(recovered, pending, errors);
    }

    private bool Reconcile(CustodyTransactionItem item, bool destinationWanted)
    {
        if (item.NamespaceItem)
            return !string.IsNullOrWhiteSpace(item.NamespaceKey)
                   && _actions.SetNamespaceHidden(
                       item.NamespaceKey,
                       destinationWanted
                           ? item.DestinationNamespaceHidden
                           : !item.DestinationNamespaceHidden);

        if (string.IsNullOrWhiteSpace(item.SourcePath) || string.IsNullOrWhiteSpace(item.DestinationPath))
            return false;
        string wanted = destinationWanted ? item.DestinationPath : item.SourcePath;
        string other = destinationWanted ? item.SourcePath : item.DestinationPath;
        bool wantedExists = _actions.Exists(wanted);
        bool otherExists = _actions.Exists(other);
        if (wantedExists && otherExists)
            return false;
        if (wantedExists)
            return true;
        if (!otherExists)
            return false;
        return _actions.Move(other, wanted);
    }

}
