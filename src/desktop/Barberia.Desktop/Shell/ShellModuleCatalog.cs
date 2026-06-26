using Barberia.Desktop.Views;

namespace Barberia.Desktop.Shell;

public static class ShellModuleCatalog
{
    public static IReadOnlyList<ShellModuleDefinition> Modules { get; } =
    [
        new(ShellModuleKey.Kiosk, "Kiosk", "Check-in", "\uE7C9", typeof(KioskPage)),
        new(ShellModuleKey.PublicDisplay, "Ticket Dashboard", "Waiting Room", "\uE8A7", typeof(PublicDisplayPage)),
        new(ShellModuleKey.BarberPanel, "Start Service", "Service", "\uE716", typeof(BarberPanelPage)),
        new(ShellModuleKey.CashBox, "Cash Box", "Payments", "\uE8C7", typeof(CashBoxPage)),
        new(ShellModuleKey.Barbers, "Barbers", "Team", "\uE716", typeof(BarbersPage)),
        new(ShellModuleKey.Backups, "Backups", "Operations", "\uE753", typeof(BackupsPage)),
        new(ShellModuleKey.TicketHistory, "Ticket History", "Archive", "\uE81C", typeof(TicketHistoryPage)),
        new(ShellModuleKey.Payroll, "Payroll", "Payroll", "\uE825", typeof(PayrollPage)),
        new(ShellModuleKey.PayrollHistory, "Payroll History", "Archive", "\uE81C", typeof(PayrollHistoryPage)),
        new(ShellModuleKey.BarberPublic, "Barber Public", "Kiosk Selection", "\uE716", typeof(BarberPublicPage)),
        new(ShellModuleKey.BarberCheckIn, "Barber Check-In", "Daily Rotation", "\uE8FB", typeof(BarberCheckInPage)),
        new(ShellModuleKey.Appointments, "Appointments", "Schedule", "\uE787", typeof(AppointmentsPage)),
        new(ShellModuleKey.LocalAdmin, "Admin Dashboard", "Operations", "\uE713", typeof(LocalAdminPage)),
        new(ShellModuleKey.Services, "Services", "Catalog", "\uE8EC", typeof(ServicesPage))
    ];
}
