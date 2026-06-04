namespace Barberia.Desktop.Services;

public sealed record KioskCheckInResult(
    string TicketNumber,
    DateTimeOffset CheckedInAt,
    string? AssignedBarberName,
    KioskCheckInStatus Status,
    string Message);

public enum KioskCheckInStatus
{
    Assigned,
    Waiting
}
