namespace Barberia.Desktop.Services;

public sealed record CashBoxDepositResult(
    string TicketNumber,
    string BarberName,
    string BarberStationCode,
    decimal Amount,
    decimal Commission,
    string ReceiptNumber,
    DateTimeOffset ClosedAt,
    string Message);
