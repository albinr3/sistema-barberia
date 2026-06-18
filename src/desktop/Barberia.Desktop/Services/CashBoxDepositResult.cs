namespace Barberia.Desktop.Services;

public sealed record CashBoxDepositResult(
    int DisplayTicketNumber,
    string InternalTicketNumber,
    string BarberName,
    string BarberStationCode,
    string ServiceName,
    decimal ServicePrice,
    decimal AdditionalAmount,
    decimal Amount,
    decimal Commission,
    string ReceiptNumber,
    DateTimeOffset ClosedAt,
    string Message,
    bool ReceiptPrinted = false,
    bool CashDrawerOpened = false,
    string? HardwareFailureMessage = null);
