namespace Barberia.Hardware.Pos;

public sealed record CashReceiptPrintJob(
    string ReceiptNumber,
    string TicketNumber,
    string BarberName,
    decimal Amount,
    decimal Commission,
    string Currency,
    DateTimeOffset CollectedAt,
    string DeviceId);
