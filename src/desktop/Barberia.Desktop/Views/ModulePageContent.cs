namespace Barberia.Desktop.Views;

public sealed record ModulePageContent(
    string Title,
    string Status,
    string IconGlyph,
    IReadOnlyList<ModuleSectionContent> Sections)
{
    public static ModulePageContent Kiosk { get; } = new(
        "Kiosco",
        "Base de entrada local",
        "\uE7C9",
        [
            new("Recepcion", "Check-in local de walk-ins", "\uE8FA"),
            new("Ticket", "Impresion y codigo QR", "\uE8A5"),
            new("Confirmacion", "Estado visual del registro", "\uE73E")
        ]);

    public static ModulePageContent PublicDisplay { get; } = new(
        "Pantalla publica",
        "Base de sala",
        "\uE8A7",
        [
            new("Espera", "Turnos en sala", "\uE81C"),
            new("Llamados", "Avisos visibles", "\uE789"),
            new("Barberos", "Disponibilidad local", "\uE716")
        ]);

    public static ModulePageContent BarberPanel { get; } = new(
        "Panel de barbero",
        "Base de atencion",
        "\uE716",
        [
            new("Sesion", "Entrada operativa", "\uE77B"),
            new("Servicio", "Atencion activa", "\uE7C9"),
            new("Ticket", "Lectura de QR", "\uE8A5")
        ]);

    public static ModulePageContent CashBox { get; } = new(
        "Autocaja",
        "Base de caja",
        "\uE8C7",
        [
            new("Efectivo", "Cobro local", "\uEAFD"),
            new("Constancia", "Comprobante impreso", "\uE8A5"),
            new("Deposito", "Registro auditable", "\uE74E")
        ]);

    public static ModulePageContent LocalAdmin { get; } = new(
        "Administracion local",
        "Base operativa",
        "\uE713",
        [
            new("Dia", "Operacion local", "\uE787"),
            new("Equipo", "Barberos y estaciones", "\uE716"),
            new("Auditoria", "Eventos locales", "\uE9D9")
        ]);

    public static ModulePageContent Reports { get; } = new(
        "Reportes",
        "Base de resumen",
        "\uE9D2",
        [
            new("Ventas", "Efectivo local", "\uE9D2"),
            new("Comisiones", "Calculo aprobado", "\uE8EC"),
            new("Cierres", "Resumen diario", "\uE8AB")
        ]);
}

public sealed record ModuleSectionContent(
    string Title,
    string Detail,
    string IconGlyph);
