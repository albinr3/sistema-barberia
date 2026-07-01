using Barberia.Desktop.Views;
using Microsoft.UI.Xaml.Controls;

namespace Barberia.Desktop.Shell;

internal static class ShellPageFactory
{
    public static Page Create(ShellModuleDefinition module, Action shellMenuRequested)
    {
        var page = Activator.CreateInstance(module.PageType) as Page
            ?? throw new InvalidOperationException($"Could not create shell page '{module.PageType.FullName}'.");

        AttachShellMenuHandler(page, shellMenuRequested);
        return page;
    }

    private static void AttachShellMenuHandler(Page page, Action shellMenuRequested)
    {
        switch (page)
        {
            case KioskPage kioskPage:
                kioskPage.ShellMenuRequested += (_, _) => shellMenuRequested();
                break;
            case PublicDisplayPage publicDisplayPage:
                publicDisplayPage.ShellMenuRequested += (_, _) => shellMenuRequested();
                break;
            case BarberPanelPage barberPanelPage:
                barberPanelPage.ShellMenuRequested += (_, _) => shellMenuRequested();
                break;
            case CashBoxPage cashBoxPage:
                cashBoxPage.ShellMenuRequested += (_, _) => shellMenuRequested();
                break;
            case LocalAdminPage localAdminPage:
                localAdminPage.ShellMenuRequested += (_, _) => shellMenuRequested();
                break;
            case BarbersPage barbersPage:
                barbersPage.ShellMenuRequested += (_, _) => shellMenuRequested();
                break;
            case ServicesPage servicesPage:
                servicesPage.ShellMenuRequested += (_, _) => shellMenuRequested();
                break;
            case TicketHistoryPage ticketHistoryPage:
                ticketHistoryPage.ShellMenuRequested += (_, _) => shellMenuRequested();
                break;
            case PayrollPage payrollPage:
                payrollPage.ShellMenuRequested += (_, _) => shellMenuRequested();
                break;
            case BarberRotationPage barberRotationPage:
                barberRotationPage.ShellMenuRequested += (_, _) => shellMenuRequested();
                break;
            case BackupsPage backupsPage:
                backupsPage.ShellMenuRequested += (_, _) => shellMenuRequested();
                break;
            case AppointmentsPage appointmentsPage:
                appointmentsPage.ShellMenuRequested += (_, _) => shellMenuRequested();
                break;
        }
    }
}
