using System.IO;
using DesktopFences.App.Services;
using DesktopFences.Core;
using DesktopFences.Core.Install;
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
        var data = new InstallerDataPolicy();
        try
        {
            using Mutex mutex = AcquireExclusiveInstance(arguments.Mode);
            try
            {
                if (InstallerCustodyRules.ReleasesCustody(arguments.Mode.ToString()))
                    ReleaseCustody();

                switch (arguments.Mode)
                {
                    case InstallerMaintenanceMode.Finalize:
                        data.SetLanguage(arguments.Language);
                        StartupRegistration.RefreshPathIfEnabled();
                        ExplorerNewMenu.RegisterFence();
                        DesktopFenceStubCleaner.TryDeleteAllOnDesktop();
                        break;
                    case InstallerMaintenanceMode.Keep:
                    case InstallerMaintenanceMode.UpgradeKeep:
                        data.SetLanguageIfCurrentSchema(arguments.Language);
                        break;
                    case InstallerMaintenanceMode.Reset:
                        data.ResetAfterRelease(arguments.Language);
                        break;
                    case InstallerMaintenanceMode.UninstallKeep:
                        StartupRegistration.RemoveForUninstall();
                        ExplorerNewMenu.UnregisterFence();
                        break;
                    case InstallerMaintenanceMode.Remove:
                        StartupRegistration.RemoveForUninstall();
                        data.RemoveAfterRelease();
                        ExplorerNewMenu.UnregisterFence();
                        break;
                }
            }
            catch (MaintenanceFailedException)
            {
                throw;
            }
            catch (Exception ex) when (IsInstanceBusy(ex))
            {
                throw new MaintenanceFailedException(
                    MaintenanceFailureKind.InstanceBusy,
                    OneLine(ex.Message));
            }
            catch (Exception ex)
            {
                throw new MaintenanceFailedException(
                    MaintenanceFailureKind.CustodyBlocked,
                    OneLine(ex.Message));
            }
            finally
            {
                try { mutex.ReleaseMutex(); } catch { }
            }

            WriteLog(data, arguments.Mode, MaintenanceFailureKind.None, "ok");
            return MaintenanceExitCodes.Success;
        }
        catch (MaintenanceFailedException ex)
        {
            if (ex.Kind == MaintenanceFailureKind.CustodyBlocked)
                data.TryArchiveWithoutDelete();
            WriteLog(data, arguments.Mode, ex.Kind, ex.Message);
            return MaintenanceFailureClassification.ExitCode(ex.Kind);
        }
        catch (Exception ex)
        {
            WriteLog(data, arguments.Mode, MaintenanceFailureKind.InstanceBusy, OneLine(ex.Message));
            return MaintenanceExitCodes.InstanceBusy;
        }
    }

    internal static bool WillReleaseCustody(InstallerMaintenanceMode mode) =>
        InstallerCustodyRules.ReleasesCustody(mode.ToString());

    private static Mutex AcquireExclusiveInstance(InstallerMaintenanceMode mode)
    {
        var mutex = new Mutex(false, @"Local\DesktopFences.SingleInstance");
        try
        {
            if (mutex.WaitOne(TimeSpan.Zero))
                return mutex;

            bool upgradeExit = InstallerCustodyRules.UsesUpgradeExit(mode.ToString());
            TimeSpan timeout = upgradeExit
                ? MaintenancePipeClient.UpgradeTimeout
                : MaintenancePipeClient.RestoreTimeout;
            string? custodyError = null;
            bool pipeOk = upgradeExit
                ? MaintenancePipeClient.RequestPrepareExitUpgrade(timeout)
                : MaintenancePipeClient.RequestPrepareExit(timeout, out custodyError);
            if (!pipeOk)
            {
                if (custodyError is not null)
                    throw new MaintenanceFailedException(MaintenanceFailureKind.CustodyBlocked, OneLine(custodyError));
                throw new MaintenanceFailedException(
                    MaintenanceFailureClassification.Classify(
                        mutexHeldByOther: true,
                        pipeOk: false,
                        recoverComplete: true,
                        outboundOk: true),
                    "O DesktopFences em execução não aceitou a saída segura.");
            }

            if (!mutex.WaitOne(timeout))
            {
                throw new MaintenanceFailedException(
                    MaintenanceFailureKind.InstanceBusy,
                    "O DesktopFences não encerrou dentro do tempo de segurança.");
            }

            return mutex;
        }
        catch (AbandonedMutexException)
        {
            return mutex;
        }
        catch (MaintenanceFailedException)
        {
            mutex.Dispose();
            throw;
        }
        catch (Exception ex)
        {
            mutex.Dispose();
            throw new MaintenanceFailedException(
                MaintenanceFailureKind.InstanceBusy,
                OneLine(ex.Message));
        }
    }

    private static void ReleaseCustody()
    {
        var store = new LayoutStore();
        var coordinator = new CustodyCoordinator(store);
        LayoutDocument document = store.LoadOrEmpty();
        DesktopRecoverySnapshot? snapshot = new DesktopRecoverySnapshotStore().Load();
        coordinator.RepairNamespaceKeys(document, snapshot);
        RecoveryReport recovery = coordinator.Recover(document);
        if (!recovery.Complete)
        {
            throw new MaintenanceFailedException(
                MaintenanceFailureClassification.Classify(
                    mutexHeldByOther: false,
                    pipeOk: true,
                    recoverComplete: false,
                    outboundOk: true),
                OneLine(string.Join(" ", recovery.Errors)));
        }

        if (document.Version == 1)
            document = coordinator.MigrateV1(document);

        List<FenceItemState> states = document.Fences.SelectMany(fence => fence.Items).ToList();
        if (!states.Any(item => item.Kind == FenceItemKind.Namespace))
        {
            IReadOnlyList<string> pending = new EmergencyRecoveryService().PendingLegacyNamespace();
            if (pending.Count > 0)
                throw new MaintenanceFailedException(MaintenanceFailureKind.CustodyBlocked,
                    "O Recovery precisa restaurar os ícones de sistema pendentes: " + string.Join(", ", pending));
        }
        if (states.Count == 0)
            return;
        List<DesktopCustodyItem> items = states.Select(DesktopCustodyItem.FromState).ToList();
        IReadOnlyList<DesktopCustodyPlan> plans = coordinator.PlanOutbound(items);
        var icons = new DesktopIconService();
        DesktopReturnReservation reservation =
            icons.ReserveReturnPositions(FenceHost.BuildReleasedPlacements(states, plans, snapshot));
        int deferred = plans.Count(plan => !FenceHost.CanReturn(plan, reservation));
        if (deferred > 0)
            throw new MaintenanceFailedException(MaintenanceFailureKind.CustodyBlocked,
                $"Não foi possível confirmar posições seguras para {deferred} item(ns). Os dados foram preservados.");
        LayoutDocument after = LayoutStore.Clone(document);
        if (!coordinator.CommitOutbound(
                document,
                after,
                CustodyOperationKind.Shutdown,
                plans,
                null,
                out string? error,
                notifyDesktopDirectory: false))
        {
            throw new MaintenanceFailedException(
                MaintenanceFailureClassification.Classify(
                    mutexHeldByOther: false,
                    pipeOk: true,
                    recoverComplete: true,
                    outboundOk: false),
                OneLine(error ?? "Não foi possível devolver todos os itens ao Desktop."));
        }

        IReadOnlyList<DesktopPlacement> placements = FenceHost.BuildReleasedPlacements(states, plans, snapshot);
        var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        FenceHost.RunPlacementRetries(
            () => icons.PlaceReservedItems(reservation, placements, handled),
            placements.Count,
            settlePasses: 3);
    }

    private static void WriteLog(
        InstallerDataPolicy data,
        InstallerMaintenanceMode mode,
        MaintenanceFailureKind kind,
        string message)
    {
        var record = new MaintenanceResultRecord(
            DateTimeOffset.UtcNow,
            mode.ToString().ToLowerInvariant(),
            kind,
            MaintenanceFailureClassification.ExitCode(kind),
            message);
        try
        {
            string path = data.MaintenanceLogPath;
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, record.ToLogText());
        }
        catch
        {
        }
    }

    private static bool IsInstanceBusy(Exception ex) =>
        ex is TimeoutException or UnauthorizedAccessException;

    private static string OneLine(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Falha na manutenção.";
        return message.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

}

internal sealed class MaintenanceFailedException : Exception
{
    public MaintenanceFailureKind Kind { get; }

    public MaintenanceFailedException(MaintenanceFailureKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }
}
