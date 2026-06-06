namespace Barberia.Hardware.Pos;

public sealed record KioskTicketPrintJob(
    int DisplayTicketNumber,
    string QrPayload,
    string CustomerName,
    IReadOnlyList<string> RequestedBarberNames,
    IReadOnlyList<string?> RequestedBarberStationCodes,
    bool AcceptsAnyBarber,
    string? AssignedBarberName,
    string? AssignedBarberStationCode,
    DateTimeOffset CheckedInAt,
    string DeviceId);
