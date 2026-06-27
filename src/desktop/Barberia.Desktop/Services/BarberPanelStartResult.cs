namespace Barberia.Desktop.Services;

public enum BarberPanelStartOutcome
{
    Started = 0,
    ReassignedToWaiting = 1,
}

public sealed record BarberPanelStartResult(
    Guid BarberId,
    int DisplayTicketNumber,
    string InternalTicketNumber,
    string BarberName,
    string BarberStationCode,
    DateTimeOffset StartedAt,
    string Message,
    BarberPanelStartOutcome Outcome = BarberPanelStartOutcome.Started);
