namespace Barberia.Desktop.Services;

public sealed record BarberPanelStartResult(
    string TicketNumber,
    string BarberName,
    DateTimeOffset StartedAt,
    string Message);
