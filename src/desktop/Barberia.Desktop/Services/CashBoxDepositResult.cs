namespace Barberia.Desktop.Services;

public sealed record CashBoxDepositResult(
    string TicketNumber,
    string BarberName,
    decimal Amount,
    decimal Commission,
    string ReceiptNumber,
    DateTimeOffset ClosedAt,
    string Message);
