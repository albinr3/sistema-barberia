namespace Barberia.Desktop.Services;

public sealed record CashBoxDepositResult(
    int DisplayTicketNumber,
    string InternalTicketNumber,
    string BarberName,
    string BarberStationCode,
    decimal Amount,
    decimal Commission,
    string ReceiptNumber,
    DateTimeOffset ClosedAt,
    string Message);
