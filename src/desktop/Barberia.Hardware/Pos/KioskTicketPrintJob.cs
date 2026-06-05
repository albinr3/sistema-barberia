namespace Barberia.Hardware.Pos;

public sealed record KioskTicketPrintJob(
    string TicketNumber,
    string QrPayload,
    string CustomerName,
    IReadOnlyList<string> RequestedBarberNames,
    bool AcceptsAnyBarber,
    string? AssignedBarberName,
    DateTimeOffset CheckedInAt,
    string DeviceId);
