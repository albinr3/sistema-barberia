using Barberia.Desktop.Shell;

namespace Barberia.Desktop.Services;

internal sealed record StationStartupPlan(
    StationRole Role,
    bool StartsDesktopBackgroundServices,
    bool StartsLanHost,
    bool RequiresLanHostBeforeOperation,
    ShellModuleKey MainModule,
    IReadOnlyList<ShellModuleKey> SecondaryModules,
    IReadOnlySet<ShellModuleKey>? VisibleShellModules);

internal static class StationStartupPlanner
{
    private static readonly IReadOnlySet<ShellModuleKey> OperationsModules = new HashSet<ShellModuleKey>
    {
        ShellModuleKey.PublicDisplay,
        ShellModuleKey.Appointments,
        ShellModuleKey.LocalAdmin,
        ShellModuleKey.Barbers,
        ShellModuleKey.Services,
        ShellModuleKey.TicketHistory,
        ShellModuleKey.Payroll,
        ShellModuleKey.Backups
    };

    public static StationStartupPlan Create(StationSettings settings)
    {
        return settings.Role switch
        {
            StationRole.KioskRotation => new StationStartupPlan(
                settings.Role,
                StartsDesktopBackgroundServices: false,
                StartsLanHost: false,
                RequiresLanHostBeforeOperation: true,
                ShellModuleKey.Kiosk,
                [ShellModuleKey.BarberRotation],
                VisibleShellModules: null),

            StationRole.CashBox => new StationStartupPlan(
                settings.Role,
                StartsDesktopBackgroundServices: false,
                StartsLanHost: false,
                RequiresLanHostBeforeOperation: true,
                ShellModuleKey.CashBox,
                [],
                VisibleShellModules: null),

            StationRole.OperationsHost => new StationStartupPlan(
                settings.Role,
                StartsDesktopBackgroundServices: true,
                StartsLanHost: true,
                RequiresLanHostBeforeOperation: false,
                ShellModuleKey.PublicDisplay,
                [ShellModuleKey.Appointments],
                OperationsModules),

            _ => new StationStartupPlan(
                StationRole.Development,
                StartsDesktopBackgroundServices: true,
                StartsLanHost: settings.StartLanHostInDevelopment,
                RequiresLanHostBeforeOperation: false,
                ShellModuleKey.Kiosk,
                [],
                VisibleShellModules: null)
        };
    }
}
