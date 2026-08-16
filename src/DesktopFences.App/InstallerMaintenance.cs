using System.IO;
using DesktopFences.App.Services;
using DesktopFences.Core;
using DesktopFences.Core.Models;
using DesktopFences.Core.Persistence;
using DesktopFences.Core.Recovery;
using DesktopFences.Core.Transactions;
using DesktopFences.Native;

namespace DesktopFences.App;

internal static class InstallerMaintenance
{
    public static int Run(InstallerMaintenanceArguments arguments)
    {
        try
        {
            using Mutex mutex = AcquireExclusiveInstance();
            var data = new InstallerDataPolicy();
            if (arguments.Mode != InstallerMaintenanceMode.Finalize)
                ReleaseCustody();

            switch (arguments.Mode)
            {
                case InstallerMaintenanceMode.Finalize:
                    data.SetLanguage(arguments.Language);
                    StartupRegistration.RefreshPathIfEnabled();
                    break;
                case InstallerMaintenanceMode.Keep:
                    data.SetLanguage(arguments.Language);
                    break;
                case InstallerMaintenanceMode.Reset:
                    data.ResetAfterRelease(arguments.Language);
                    break;
                case InstallerMaintenanceMode.UninstallKeep:
                    StartupRegistration.RemoveForUninstall();
                    break;
                case InstallerMaintenanceMode.Remove:
                    StartupRegistration.RemoveForUninstall();
                    data.RemoveAfterRelease();
                    break;
            }

            try { mutex.ReleaseMutex(); } catch { }
            return 0;
        }
        catch
        {
            return 1;
        }
    }

    private static Mutex AcquireExclusiveInstance()
    {
        var mutex = new Mutex(false, @"Local\DesktopFences.SingleInstance");
        try
        {
            if (mutex.WaitOne(TimeSpan.Zero))
                return mutex;
            if (!MaintenancePipeClient.RequestPrepareExit(TimeSpan.FromSeconds(10)))
                throw new IOException("O DesktopFences em execução não aceitou a saída segura.");
            if (!mutex.WaitOne(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("O DesktopFences não encerrou dentro do tempo de segurança.");
            return mutex;
        }
        catch (AbandonedMutexException)
        {
            return mutex;
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    private static void ReleaseCustody()
    {
        var store = new LayoutStore();
        var coordinator = new CustodyCoordinator(store);
        LayoutDocument document = store.LoadOrEmpty();
        RecoveryReport recovery = coordinator.Recover(document);
        if (!recovery.Complete)
            throw new InvalidDataException(string.Join(Environment.NewLine, recovery.Errors));
        if (document.Version == 1)
            document = coordinator.MigrateV1(document);

        List<FenceItemState> states = document.Fences.SelectMany(fence => fence.Items).ToList();
        if (states.Count == 0)
            return;
        List<DesktopCustodyItem> items = states.Select(ToCustodyItem).ToList();
        IReadOnlyList<DesktopCustodyPlan> plans = coordinator.PlanOutbound(items);
        LayoutDocument after = LayoutStore.Clone(document);
        if (!coordinator.CommitOutbound(
                document,
                after,
                CustodyOperationKind.Shutdown,
                plans,
                null,
                out string? error))
            throw new IOException(error ?? "Não foi possível devolver todos os itens ao Desktop.");

        DesktopRecoverySnapshot? snapshot = new DesktopRecoverySnapshotStore().Load();
        IReadOnlyList<DesktopPlacement> placements = FenceHost.BuildReleasedPlacements(states, plans, snapshot);
        var icons = new DesktopIconService();
        FenceHost.RunPlacementRetries(
            () => icons.PlaceRevealedItems(placements),
            placements.Count,
            wait: () => Thread.Sleep(120));
    }

    private static DesktopCustodyItem ToCustodyItem(FenceItemState item)
    {
        string? runtime = item.Kind == FenceItemKind.Stored && !string.IsNullOrWhiteSpace(item.StorageName)
            ? FenceItemStore.PayloadPath(item.ItemId, item.StorageName)
            : item.OriginalPath ?? item.Name;
        if (item.Kind == FenceItemKind.Stored
            && !string.IsNullOrWhiteSpace(item.OriginalPath)
            && !File.Exists(runtime) && !Directory.Exists(runtime))
            runtime = item.OriginalPath;
        return new DesktopCustodyItem(
            item.ItemId,
            item.Kind,
            item.Name,
            runtime,
            item.OriginalPath,
            item.StorageName);
    }
}
