using DesktopFences.Core.Models;
using DesktopFences.Core.Transactions;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class TransactionJournalTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "df-journal-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Save_UpdatesOneAtomicJournalPerOperation()
    {
        var store = new TransactionJournalStore(_dir);
        CustodyTransaction transaction = Transaction(CustodyTransactionState.Prepared);
        store.Save(transaction);
        transaction.State = CustodyTransactionState.PayloadChanged;
        store.Save(transaction);

        store.LoadAll().Should().ContainSingle().Which.State.Should().Be(CustodyTransactionState.PayloadChanged);
        Directory.EnumerateFiles(_dir, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public void Recovery_RollsBackPreparedPhysicalChangeAndIsIdempotent()
    {
        var store = new TransactionJournalStore(_dir);
        CustodyTransaction transaction = Transaction(CustodyTransactionState.Prepared);
        store.Save(transaction);
        var actions = new FakeActions(transaction.Items.Single().DestinationPath!);
        var recovery = new TransactionRecovery(store, actions);

        RecoveryReport first = recovery.Recover(new LayoutDocument());
        RecoveryReport second = recovery.Recover(new LayoutDocument());

        first.Complete.Should().BeTrue();
        actions.Paths.Should().Contain(transaction.Items.Single().SourcePath!);
        actions.Paths.Should().NotContain(transaction.Items.Single().DestinationPath!);
        second.Recovered.Should().Be(0);
        second.Pending.Should().Be(0);
        second.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Recovery_RollsForwardWhenLayoutRevisionProvesCommit()
    {
        var store = new TransactionJournalStore(_dir);
        CustodyTransaction transaction = Transaction(CustodyTransactionState.PayloadChanged);
        transaction.LayoutRevisionAfter = 7;
        store.Save(transaction);
        var actions = new FakeActions(transaction.Items.Single().SourcePath!);

        RecoveryReport report = new TransactionRecovery(store, actions)
            .Recover(new LayoutDocument { Revision = 7 });

        report.Complete.Should().BeTrue();
        actions.Paths.Should().Contain(transaction.Items.Single().DestinationPath!);
    }

    [Fact]
    public void Recovery_PreservesJournalWhenNeitherCopyExists()
    {
        var store = new TransactionJournalStore(_dir);
        CustodyTransaction transaction = Transaction(CustodyTransactionState.Prepared);
        store.Save(transaction);

        RecoveryReport report = new TransactionRecovery(store, new FakeActions())
            .Recover(new LayoutDocument());

        report.Pending.Should().Be(1);
        store.LoadAll().Should().ContainSingle();
    }

    [Fact]
    public void Recovery_PreservesBothCopiesAndJournalWhenPhysicalStateIsAmbiguous()
    {
        var store = new TransactionJournalStore(_dir);
        CustodyTransaction transaction = Transaction(CustodyTransactionState.Prepared);
        store.Save(transaction);
        CustodyTransactionItem item = transaction.Items.Single();
        var actions = new FakeActions(item.SourcePath!, item.DestinationPath!);

        RecoveryReport report = new TransactionRecovery(store, actions)
            .Recover(new LayoutDocument());

        report.Pending.Should().Be(1);
        actions.Paths.Should().Contain(item.SourcePath!);
        actions.Paths.Should().Contain(item.DestinationPath!);
        store.LoadAll().Should().ContainSingle();
    }

    [Theory]
    [InlineData(CustodyTransactionState.Prepared, false)]
    [InlineData(CustodyTransactionState.LayoutCommitted, true)]
    public void Recovery_ReconcilesNamespaceDirection(
        CustodyTransactionState state,
        bool expectedHidden)
    {
        var store = new TransactionJournalStore(_dir);
        CustodyTransaction transaction = Transaction(state);
        transaction.Items =
        [
            new CustodyTransactionItem
            {
                ItemId = Guid.NewGuid(),
                Name = "Lixeira",
                NamespaceItem = true,
                NamespaceKey = "::{CLSID}",
                DestinationNamespaceHidden = true
            }
        ];
        store.Save(transaction);
        var actions = new FakeActions();

        new TransactionRecovery(store, actions).Recover(new LayoutDocument());

        actions.LastNamespaceHidden.Should().Be(expectedHidden);
    }

    [Theory]
    [InlineData(CustodyTransactionState.Prepared, false)]
    [InlineData(CustodyTransactionState.PayloadChanged, false)]
    [InlineData(CustodyTransactionState.FailedRecoverable, false)]
    [InlineData(CustodyTransactionState.LayoutCommitted, true)]
    [InlineData(CustodyTransactionState.Completed, true)]
    public void Recovery_ReconcilesEveryDurableState(
        CustodyTransactionState state,
        bool destinationExpected)
    {
        var store = new TransactionJournalStore(_dir);
        CustodyTransaction transaction = Transaction(state);
        store.Save(transaction);
        string existing = destinationExpected
            ? transaction.Items.Single().SourcePath!
            : transaction.Items.Single().DestinationPath!;
        var actions = new FakeActions(existing);

        RecoveryReport report = new TransactionRecovery(store, actions)
            .Recover(new LayoutDocument());

        report.Complete.Should().BeTrue();
        string expected = destinationExpected
            ? transaction.Items.Single().DestinationPath!
            : transaction.Items.Single().SourcePath!;
        actions.Paths.Should().Contain(expected);
    }

    private static CustodyTransaction Transaction(CustodyTransactionState state) => new()
    {
        Operation = CustodyOperationKind.Inbound,
        State = state,
        Items =
        [
            new CustodyTransactionItem
            {
                ItemId = Guid.NewGuid(),
                Name = "arquivo.txt",
                SourcePath = @"C:\Desktop\arquivo.txt",
                DestinationPath = @"C:\Store\id\arquivo.txt"
            }
        ]
    };

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private sealed class FakeActions(params string[] paths) : ICustodyRecoveryActions
    {
        public HashSet<string> Paths { get; } = new(paths, StringComparer.OrdinalIgnoreCase);
        public bool? LastNamespaceHidden { get; private set; }

        public bool Exists(string path) => Paths.Contains(path);

        public bool Move(string source, string destination)
        {
            if (!Paths.Remove(source) || Paths.Contains(destination))
                return false;
            Paths.Add(destination);
            return true;
        }

        public bool SetNamespaceHidden(string key, bool hidden)
        {
            LastNamespaceHidden = hidden;
            return true;
        }
    }
}
