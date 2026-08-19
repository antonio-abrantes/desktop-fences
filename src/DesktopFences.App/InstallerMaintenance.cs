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
            bool pipeOk = upgradeExit
                ? MaintenancePipeClient.RequestPrepareExitUpgrade(timeout)
                : MaintenancePipeClient.RequestPrepareExit(timeout);
            if (!pipeOk)
            {
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
        {
            throw new MaintenanceFailedException(
                MaintenanceFailureClassification.Classify(
                    mutexHeldByOther: false,
                    pipeOk: true,
                    recoverComplete: true,
                    outboundOk: false),
                OneLine(error ?? "Não foi possível devolver todos os itens ao Desktop."));
        }

        DesktopRecoverySnapshot? snapshot = new DesktopRecoverySnapshotStore().Load();
        IReadOnlyList<DesktopPlacement> placements = FenceHost.BuildReleasedPlacements(states, plans, snapshot);
        var icons = new DesktopIconService();
        FenceHost.RunPlacementRetries(
            () => icons.PlaceRevealedItems(placements),
            placements.Count,
            wait: () => Thread.Sleep(120));
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

internal sealed class MaintenanceFailedException : Exception
{
    public MaintenanceFailureKind Kind { get; }

    public MaintenanceFailedException(MaintenanceFailureKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }
}
