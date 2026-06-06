namespace Barberia.Desktop.Services;

public sealed record BarberPanelStartResult(
    int DisplayTicketNumber,
    string InternalTicketNumber,
    string BarberName,
    string BarberStationCode,
    DateTimeOffset StartedAt,
    string Message);
