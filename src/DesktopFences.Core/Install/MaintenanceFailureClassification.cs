namespace DesktopFences.Core.Install;

public enum MaintenanceFailureKind
{
    None,
    InstanceBusy,
    CustodyBlocked,
    InvalidRequest
}

public static class MaintenanceExitCodes
{
    public const int Success = 0;
    public const int InstanceBusy = 1;
    public const int InvalidRequest = 2;
    public const int CustodyBlocked = 3;
}

/// <summary>
/// Tabela da spec: instância ocupada vs custódia partida. Usada pelo helper de manutenção.
/// </summary>
public static class MaintenanceFailureClassification
{
    public static MaintenanceFailureKind Classify(
        bool mutexHeldByOther,
        bool pipeOk,
        bool recoverComplete,
        bool outboundOk)
    {
        if (mutexHeldByOther && !pipeOk)
            return MaintenanceFailureKind.InstanceBusy;

        if (!recoverComplete || !outboundOk)
            return MaintenanceFailureKind.CustodyBlocked;

        return MaintenanceFailureKind.None;
    }

    public static int ExitCode(MaintenanceFailureKind kind) => kind switch
    {
        MaintenanceFailureKind.None => MaintenanceExitCodes.Success,
        MaintenanceFailureKind.InstanceBusy => MaintenanceExitCodes.InstanceBusy,
        MaintenanceFailureKind.InvalidRequest => MaintenanceExitCodes.InvalidRequest,
        MaintenanceFailureKind.CustodyBlocked => MaintenanceExitCodes.CustodyBlocked,
        _ => MaintenanceExitCodes.CustodyBlocked
    };
}

public static class InstallerCustodyRules
{
    public static bool ReleasesCustody(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return false;

        return mode.Equals("reset", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("uninstallkeep", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("remove", StringComparison.OrdinalIgnoreCase);
    }

    public static bool UsesUpgradeExit(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return false;

        return mode.Equals("upgradekeep", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("keep", StringComparison.OrdinalIgnoreCase);
    }
}
