using Barberia.Core.Domain;

namespace Barberia.Desktop.Services;

public sealed record KioskCheckInSnapshot(
    DateTimeOffset LoadedAt,
    IReadOnlyList<Barber> Barbers);

public sealed record KioskCheckInResult(
    int DisplayTicketNumber,
    string InternalTicketNumber,
    string QrPayload,
    string CustomerName,
    DateTimeOffset CheckedInAt,
    string? AssignedBarberName,
    string? AssignedBarberStationCode,
    IReadOnlyList<string> RequestedBarberNames,
    IReadOnlyList<string?> RequestedBarberStationCodes,
    bool AcceptsAnyBarber,
    KioskCheckInStatus Status,
    string Message);

public enum KioskCheckInStatus
{
    Assigned,
    Waiting
}
