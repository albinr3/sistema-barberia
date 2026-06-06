namespace Barberia.Hardware.Pos;

public sealed record CashReceiptPrintJob(
    string ReceiptNumber,
    int DisplayTicketNumber,
    string BarberName,
    string BarberStationCode,
    decimal Amount,
    decimal Commission,
    string Currency,
    DateTimeOffset CollectedAt,
    string DeviceId);
