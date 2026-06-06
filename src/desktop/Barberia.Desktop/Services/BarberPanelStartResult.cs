namespace Barberia.Desktop.Services;

public sealed record BarberPanelStartResult(
    string TicketNumber,
    string BarberName,
    string BarberStationCode,
    DateTimeOffset StartedAt,
    string Message);
