using Barberia.Desktop.Views;

namespace Barberia.Desktop.Shell;

public static class ShellModuleCatalog
{
    public static IReadOnlyList<ShellModuleDefinition> Modules { get; } =
    [
        new(ShellModuleKey.Kiosk, "Kiosk", "Check-in", "\uE7C9", typeof(KioskPage)),
        new(ShellModuleKey.PublicDisplay, "Public Display", "Waiting Room", "\uE8A7", typeof(PublicDisplayPage)),
        new(ShellModuleKey.BarberPanel, "Barber Panel", "Service", "\uE716", typeof(BarberPanelPage)),
        new(ShellModuleKey.CashBox, "Cash Box", "Payments", "\uE8C7", typeof(CashBoxPage)),
        new(ShellModuleKey.LocalAdmin, "Local Admin", "Operations", "\uE713", typeof(LocalAdminPage)),
        new(ShellModuleKey.Barbers, "Barbers", "Team", "\uE716", typeof(BarbersPage)),
        new(ShellModuleKey.Services, "Services", "Catalog", "\uE8EC", typeof(ServicesPage)),
        new(ShellModuleKey.Reports, "Reports", "Summary", "\uE9D2", typeof(ReportsPage)),
        new(ShellModuleKey.TicketHistory, "Ticket History", "Archive", "\uE81C", typeof(TicketHistoryPage)),
        new(ShellModuleKey.Payroll, "Payroll", "Nómina", "\uE825", typeof(PayrollPage))
    ];
}
