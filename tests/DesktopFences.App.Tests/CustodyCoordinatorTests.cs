using System.IO;
using System.Text.Json;
using DesktopFences.Core.Models;
using DesktopFences.Core.Fences;
using DesktopFences.Core.Persistence;
using DesktopFences.Core.Transactions;
using DesktopFences.Native;
using FluentAssertions;
using Xunit;

namespace DesktopFences.App.Tests;

public sealed class CustodyCoordinatorTests
{
    [Fact]
    public void Inbound_OneHundredItems_UsesOneBatchOneLayoutCommitOneUiApplyAndOneNotify()
    {
        TestContext context = CreateContext();
        IReadOnlyList<DesktopCustodyPlan> plans = Plans(100);
        int uiApplies = 0;

        bool success = context.Coordinator.CommitInbound(
            Document(revision: 3),
            Document(revision: 3),
            CustodyOperationKind.Inbound,
            plans,
            () => uiApplies++,
            out string? error);

        success.Should().BeTrue(error);
        context.Batch.InboundExecutions.Should().Be(1);
        context.Batch.OutboundExecutions.Should().Be(0);
        context.Layouts.SaveCount.Should().Be(1);
        context.Journals.SavedStates.Should().Equal(
            CustodyTransactionState.Prepared,
            CustodyTransactionState.PayloadChanged,
            CustodyTransactionState.LayoutCommitted,
            CustodyTransactionState.Completed);
        context.Journals.DeleteCount.Should().Be(1);
        uiApplies.Should().Be(1);
        context.Batch.FlushCount.Should().Be(1);
    }

    [Fact]
    public void Outbound_UsesTheSameStageBudgetAsInbound()
    {
        TestContext context = CreateContext();

        bool success = context.Coordinator.CommitOutbound(
            Document(revision: 8),
            Document(revision: 8),
            CustodyOperationKind.Shutdown,
            Plans(50),
            () => context.UiApplies++,
            out string? error);

        success.Should().BeTrue(error);
        context.Batch.OutboundExecutions.Should().Be(1);
        context.Layouts.SaveCount.Should().Be(1);
        context.Journals.SaveCount.Should().Be(4);
        context.UiApplies.Should().Be(1);
        context.Batch.FlushCount.Should().Be(1);
    }

    [Theory]
    [InlineData(CustodyCheckpoint.Prepared, CustodyTransactionState.Prepared, 0, 0, 0)]
    [InlineData(CustodyCheckpoint.PayloadExecuted, CustodyTransactionState.Prepared, 1, 0, 0)]
    [InlineData(CustodyCheckpoint.PayloadChanged, CustodyTransactionState.PayloadChanged, 1, 0, 0)]
    [InlineData(CustodyCheckpoint.LayoutSaved, CustodyTransactionState.PayloadChanged, 1, 1, 0)]
    [InlineData(CustodyCheckpoint.LayoutCommitted, CustodyTransactionState.LayoutCommitted, 1, 1, 0)]
    [InlineData(CustodyCheckpoint.UiApplied, CustodyTransactionState.LayoutCommitted, 1, 1, 1)]
    [InlineData(CustodyCheckpoint.Completed, CustodyTransactionState.Completed, 1, 1, 1)]
    public void InjectedCrash_LeavesTheExpectedDurableEvidence(
        CustodyCheckpoint checkpoint,
        CustodyTransactionState expectedState,
        int batchExecutions,
        int layoutSaves,
        int uiApplies)
    {
        var faults = new ThrowingFaultInjector(checkpoint);
        TestContext context = CreateContext(faults: faults);
        int applied = 0;

        Action act = () => context.Coordinator.CommitInbound(
            Document(revision: 1),
            Document(revision: 1),
            CustodyOperationKind.Inbound,
            Plans(2),
            () => applied++,
            out _);

        act.Should().Throw<InjectedCrashException>();
        context.Journals.Current.Should().NotBeNull();
        context.Journals.Current!.State.Should().Be(expectedState);
        context.Batch.InboundExecutions.Should().Be(batchExecutions);
        context.Layouts.SaveCount.Should().Be(layoutSaves);
        applied.Should().Be(uiApplies);
        context.Batch.FlushCount.Should().Be(0);
        context.Journals.DeleteCount.Should().Be(0);
    }

    [Theory]
    [InlineData(CustodyCheckpoint.Prepared, false, false)]
    [InlineData(CustodyCheckpoint.PayloadExecuted, true, false)]
    [InlineData(CustodyCheckpoint.PayloadChanged, true, false)]
    [InlineData(CustodyCheckpoint.LayoutSaved, true, true)]
    [InlineData(CustodyCheckpoint.LayoutCommitted, true, true)]
    [InlineData(CustodyCheckpoint.UiApplied, true, true)]
    [InlineData(CustodyCheckpoint.Completed, true, true)]
    public void InjectedCrash_IsReconciledFromJournalAndLayoutRevision(
        CustodyCheckpoint checkpoint,
        bool payloadReachedDestination,
        bool layoutWasCommitted)
    {
        var faults = new ThrowingFaultInjector(checkpoint);
        TestContext context = CreateContext(faults: faults);
        DesktopCustodyPlan plan = Plans(1).Single();
        LayoutDocument before = Document(revision: 1);
        LayoutDocument after = Document(revision: 1);
        Action crash = () => context.Coordinator.CommitInbound(
            before, after, CustodyOperationKind.Inbound, [plan], null, out _);
        crash.Should().Throw<InjectedCrashException>();
        var actions = new PathRecoveryActions(
            payloadReachedDestination ? plan.DestinationPath! : plan.SourcePath!);
        LayoutDocument recoveredLayout = Document(layoutWasCommitted ? after.Revision : before.Revision);

        RecoveryReport report = new TransactionRecovery(context.Journals, actions)
            .Recover(recoveredLayout);

        report.Complete.Should().BeTrue();
        string expectedPath = layoutWasCommitted ? plan.DestinationPath! : plan.SourcePath!;
        actions.Paths.Should().Contain(expectedPath);
        context.Journals.Current.Should().BeNull();
    }

    [Fact]
    public void LayoutSaveFailure_CompensatesPayloadAndDoesNotApplyUi()
    {
        TestContext context = CreateContext();
        context.Layouts.ThrowOnSave = true;
        int uiApplies = 0;

        bool success = context.Coordinator.CommitInbound(
            Document(revision: 1),
            Document(revision: 1),
            CustodyOperationKind.Inbound,
            Plans(3),
            () => uiApplies++,
            out string? error);

        success.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
        context.Batch.Compensations.Should().Be(1);
        uiApplies.Should().Be(0);
        context.Batch.FlushCount.Should().Be(0);
        context.Journals.DeleteCount.Should().Be(1);
    }

    [Fact]
    public void PhysicalBatchFailure_DoesNotCommitLayoutOrApplyUi()
    {
        TestContext context = CreateContext();
        context.Batch.FailInbound = true;
        int uiApplies = 0;

        bool success = context.Coordinator.CommitInbound(
            Document(revision: 2),
            Document(revision: 2),
            CustodyOperationKind.Inbound,
            Plans(3),
            () => uiApplies++,
            out _);

        success.Should().BeFalse();
        context.Layouts.SaveCount.Should().Be(0);
        uiApplies.Should().Be(0);
        context.Journals.Current!.State.Should().Be(CustodyTransactionState.FailedRecoverable);
        context.Journals.DeleteCount.Should().Be(0);
    }

    [Fact]
    public void ReentryWithSameItemId_IsRejectedWhileOuterOperationIsActive()
    {
        TestContext context = CreateContext();
        IReadOnlyList<DesktopCustodyPlan> plans = Plans(1);
        bool? nestedSuccess = null;
        string? nestedError = null;

        bool outerSuccess = context.Coordinator.CommitInbound(
            Document(revision: 1),
            Document(revision: 1),
            CustodyOperationKind.Inbound,
            plans,
            () => nestedSuccess = context.Coordinator.CommitInbound(
                Document(revision: 2),
                Document(revision: 2),
                CustodyOperationKind.Inbound,
                plans,
                null,
                out nestedError),
            out _);

        outerSuccess.Should().BeTrue();
        nestedSuccess.Should().BeFalse();
        nestedError.Should().Contain("já participa");
        context.Batch.InboundExecutions.Should().Be(1);
    }

    [Fact]
    public void CaptureDesktop_DelegatesExactlyOnceToSnapshotSource()
    {
        TestContext context = CreateContext();
        var source = new FakeSnapshotSource();

        DesktopSnapshot snapshot = context.Coordinator.CaptureDesktop(source);

        snapshot.Connected.Should().BeTrue();
        source.CaptureCount.Should().Be(1);
    }

    [Fact]
    public void ShutdownAfterMetadataTransfer_RestoresEveryTransferredItemInOneOutboundBatch()
    {
        Guid sourceId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        List<FenceItemState> items = Enumerable.Range(1, 10).Select(i => new FenceItemState
        {
            ItemId = GuidFromInt(i),
            Kind = FenceItemKind.Stored,
            Name = $"item-{i}.txt",
            StorageName = $"item-{i}.txt",
            OriginalPath = $@"C:\Desktop\item-{i}.txt"
        }).ToList();
        LayoutDocument beforeTransfer = new()
        {
            Revision = 5,
            Fences =
            [
                new FenceState { Id = sourceId, Items = items },
                new FenceState { Id = targetId }
            ]
        };
        LayoutDocument transferred = FenceOwnership.Transfer(
            beforeTransfer, sourceId, targetId, items.Select(i => i.ItemId).ToList(), 0);
        TestContext context = CreateContext();
        IReadOnlyList<DesktopCustodyPlan> plans = Plans(10);

        bool success = context.Coordinator.CommitOutbound(
            transferred,
            LayoutStore.Clone(transferred),
            CustodyOperationKind.Shutdown,
            plans,
            null,
            out string? error);

        success.Should().BeTrue(error);
        transferred.Fences.Single(f => f.Id == targetId).Items.Select(i => i.ItemId)
            .Should().BeEquivalentTo(context.Batch.LastOutboundPlans.Select(p => p.ItemId));
        context.Batch.OutboundExecutions.Should().Be(1);
    }

    [Fact]
    public void MigrationV1_UsesJournalAndPreservesV1AsLayoutBackup()
    {
        string root = Path.Combine(Path.GetTempPath(), "df-app-migration-" + Guid.NewGuid().ToString("N"));
        try
        {
            string itemsRoot = Path.Combine(root, "Items");
            Guid fenceId = Guid.NewGuid();
            string legacyFolder = Path.Combine(itemsRoot, fenceId.ToString("D"));
            Directory.CreateDirectory(legacyFolder);
            string payload = Path.Combine(legacyFolder, "arquivo.txt");
            File.WriteAllText(payload, "payload");
            string restoredDesktopFolder = Path.Combine(root, "Desktop");
            Directory.CreateDirectory(restoredDesktopFolder);
            string restoredPayload = Path.Combine(restoredDesktopFolder, "restaurado.lnk");
            File.WriteAllText(restoredPayload, "shortcut");
            string staleStorePath = Path.Combine(legacyFolder, "restaurado.lnk");
            string layoutPath = Path.Combine(root, "layout.json");
            var legacy = new LayoutDocument
            {
                Version = 1,
                Fences =
                [
                    new FenceState
                    {
                        Id = fenceId,
                        Items =
                        [
                            new FenceItemState { Name = "arquivo.txt", Path = payload },
                            new FenceItemState
                            {
                                Name = "restaurado",
                                Path = staleStorePath,
                                OriginalPath = restoredPayload
                            }
                        ]
                    }
                ]
            };
            File.WriteAllText(layoutPath, JsonSerializer.Serialize(legacy));
            var layouts = new LayoutStore(layoutPath);
            var journals = new FakeJournalStore();
            var batch = new FakeBatch();
            var coordinator = new CustodyCoordinator(
                layouts,
                journals,
                batch,
                new FakeRecoveryActions(),
                NoCustodyFaults.Instance,
                itemsRoot);

            LayoutDocument migrated = coordinator.MigrateV1(layouts.LoadOrEmpty());

            migrated.Version.Should().Be(2);
            migrated.Fences.Single().Items.Should().HaveCount(2)
                .And.OnlyContain(item => item.ItemId != Guid.Empty);
            batch.InboundExecutions.Should().Be(1);
            batch.LastInboundPlans.Should().ContainSingle()
                .Which.SourcePath.Should().Be(payload);
            journals.SavedStates.Should().Equal(
                CustodyTransactionState.Prepared,
                CustodyTransactionState.PayloadChanged,
                CustodyTransactionState.LayoutCommitted,
                CustodyTransactionState.Completed);
            layouts.LoadOrEmpty().Version.Should().Be(2);
            File.ReadAllText(layouts.BackupPath).Should().Contain("\"Version\":1");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static TestContext CreateContext(ICustodyFaultInjector? faults = null)
    {
        var layouts = new FakeLayoutStore();
        var journals = new FakeJournalStore();
        var batch = new FakeBatch();
        var coordinator = new CustodyCoordinator(
            layouts,
            journals,
            batch,
            new FakeRecoveryActions(),
            faults ?? NoCustodyFaults.Instance);
        return new TestContext(coordinator, layouts, journals, batch);
    }

    private static LayoutDocument Document(long revision) => new()
    {
        Revision = revision,
        Fences = [new FenceState { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") }]
    };

    private static IReadOnlyList<DesktopCustodyPlan> Plans(int count) =>
        Enumerable.Range(1, count).Select(i =>
        {
            Guid id = GuidFromInt(i);
            return new DesktopCustodyPlan(
                id,
                FenceItemKind.Stored,
                $"item-{i}.txt",
                $@"C:\Desktop\item-{i}.txt",
                $@"C:\Store\{id:D}\item-{i}.txt",
                $@"C:\Desktop\item-{i}.txt",
                $"item-{i}.txt",
                null);
        }).ToList();

    private static Guid GuidFromInt(int value)
    {
        byte[] bytes = new byte[16];
        BitConverter.GetBytes(value).CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    private sealed record TestContext(
        CustodyCoordinator Coordinator,
        FakeLayoutStore Layouts,
        FakeJournalStore Journals,
        FakeBatch Batch)
    {
        public int UiApplies { get; set; }
    }

    private sealed class FakeLayoutStore : ILayoutStore
    {
        public int SaveCount { get; private set; }
        public bool ThrowOnSave { get; set; }
        public long? LastRevision { get; private set; }

        public void Save(LayoutDocument document)
        {
            SaveCount++;
            if (ThrowOnSave)
                throw new IOException("save injetado");
            LastRevision = document.Revision;
        }
    }

    private sealed class FakeJournalStore : ITransactionJournalStore
    {
        public string DirectoryPath => @"C:\Fake\Transactions";
        public int SaveCount => SavedStates.Count;
        public int DeleteCount { get; private set; }
        public List<CustodyTransactionState> SavedStates { get; } = [];
        public CustodyTransaction? Current { get; private set; }

        public void Save(CustodyTransaction transaction)
        {
            SavedStates.Add(transaction.State);
            Current = Snapshot(transaction);
        }

        public IReadOnlyList<CustodyTransaction> LoadAll() => Current is null ? [] : [Current];

        public void Delete(Guid operationId)
        {
            DeleteCount++;
            Current = null;
        }

        private static CustodyTransaction Snapshot(CustodyTransaction source) => new()
        {
            OperationId = source.OperationId,
            Operation = source.Operation,
            State = source.State,
            CreatedUtc = source.CreatedUtc,
            UpdatedUtc = source.UpdatedUtc,
            LayoutRevisionBefore = source.LayoutRevisionBefore,
            LayoutRevisionAfter = source.LayoutRevisionAfter,
            Error = source.Error,
            Items = source.Items.Select(item => new CustodyTransactionItem
            {
                ItemId = item.ItemId,
                SourceFenceId = item.SourceFenceId,
                TargetFenceId = item.TargetFenceId,
                Name = item.Name,
                SourcePath = item.SourcePath,
                DestinationPath = item.DestinationPath,
                NamespaceItem = item.NamespaceItem,
                NamespaceKey = item.NamespaceKey,
                DestinationNamespaceHidden = item.DestinationNamespaceHidden
            }).ToList()
        };
    }

    private sealed class FakeBatch : IDesktopCustodyBatch
    {
        public int InboundExecutions { get; private set; }
        public int OutboundExecutions { get; private set; }
        public int Compensations { get; private set; }
        public int FlushCount { get; private set; }
        public bool FailInbound { get; set; }
        public IReadOnlyList<DesktopCustodyPlan> LastInboundPlans { get; private set; } = [];
        public IReadOnlyList<DesktopCustodyPlan> LastOutboundPlans { get; private set; } = [];

        public IReadOnlyList<DesktopCustodyPlan> PlanInbound(IEnumerable<DesktopCustodyItem> items) => [];

        public IReadOnlyList<DesktopCustodyPlan> PlanOutbound(IEnumerable<DesktopCustodyItem> items) => [];

        public DesktopCustodyBatchResult ExecuteInbound(IReadOnlyList<DesktopCustodyPlan> plans)
        {
            InboundExecutions++;
            LastInboundPlans = plans;
            return FailInbound
                ? DesktopCustodyBatchResult.Failed("falha física injetada")
                : new DesktopCustodyBatchResult(true, plans, null);
        }

        public DesktopCustodyBatchResult ExecuteOutbound(IReadOnlyList<DesktopCustodyPlan> plans)
        {
            OutboundExecutions++;
            LastOutboundPlans = plans;
            return new DesktopCustodyBatchResult(true, plans, null);
        }

        public bool Compensate(IReadOnlyList<DesktopCustodyPlan> plans, bool wasInbound)
        {
            Compensations++;
            return true;
        }

        public void FlushShell() => FlushCount++;
    }

    private sealed class FakeRecoveryActions : ICustodyRecoveryActions
    {
        public bool Exists(string path) => false;
        public bool Move(string source, string destination) => true;
        public bool SetNamespaceHidden(string key, bool hidden) => true;
    }

    private sealed class PathRecoveryActions(params string[] paths) : ICustodyRecoveryActions
    {
        public HashSet<string> Paths { get; } = new(paths, StringComparer.OrdinalIgnoreCase);

        public bool Exists(string path) => Paths.Contains(path);

        public bool Move(string source, string destination)
        {
            if (!Paths.Remove(source) || Paths.Contains(destination))
                return false;
            Paths.Add(destination);
            return true;
        }

        public bool SetNamespaceHidden(string key, bool hidden) => true;
    }

    private sealed class FakeSnapshotSource : IDesktopSnapshotSource
    {
        public int CaptureCount { get; private set; }

        public DesktopSnapshot Capture()
        {
            CaptureCount++;
            return new DesktopSnapshot(true, "0x1", [], null);
        }
    }

    private sealed class ThrowingFaultInjector(CustodyCheckpoint target) : ICustodyFaultInjector
    {
        public void Hit(CustodyCheckpoint checkpoint, CustodyTransaction transaction)
        {
            if (checkpoint == target)
                throw new InjectedCrashException(checkpoint);
        }
    }

    private sealed class InjectedCrashException(CustodyCheckpoint checkpoint)
        : Exception($"Crash injetado em {checkpoint}");
}
