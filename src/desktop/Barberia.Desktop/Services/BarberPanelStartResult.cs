namespace Barberia.Desktop.Services;

public sealed record BarberPanelStartResult(
    Guid BarberId,
    int DisplayTicketNumber,
    string InternalTicketNumber,
    string BarberName,
    string BarberStationCode,
    DateTimeOffset StartedAt,
    string Message);
