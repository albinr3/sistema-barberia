using Barberia.Desktop.Views;

namespace Barberia.Desktop.Shell;

public static class ShellModuleCatalog
{
    public static IReadOnlyList<ShellModuleDefinition> Modules { get; } =
    [
        new(ShellModuleKey.Kiosk, "Kiosco", "Entrada", "\uE7C9", typeof(KioskPage)),
        new(ShellModuleKey.PublicDisplay, "Pantalla publica", "Sala", "\uE8A7", typeof(PublicDisplayPage)),
        new(ShellModuleKey.BarberPanel, "Panel de barbero", "Atencion", "\uE716", typeof(BarberPanelPage)),
        new(ShellModuleKey.CashBox, "Autocaja", "Caja", "\uE8C7", typeof(CashBoxPage)),
        new(ShellModuleKey.LocalAdmin, "Administracion local", "Operacion", "\uE713", typeof(LocalAdminPage)),
        new(ShellModuleKey.Reports, "Reportes", "Resumen", "\uE9D2", typeof(ReportsPage))
    ];
}
