namespace Barberia.Desktop.Services;

public sealed record PendingServicePaymentResult(
    int DisplayTicketNumber,
    string InternalTicketNumber,
    string BarberName,
    string BarberStationCode,
    string ServiceName,
    decimal ServicePrice,
    decimal AdditionalAmount,
    decimal Amount,
    decimal Commission,
    DateTimeOffset PendingAt,
    string Message);

public sealed record PendingPaymentCollectorLookupResult(
    Guid BarberId,
    string BarberName,
    string BarberStationCode);

public sealed record PendingPaymentCollectionResult(
    int PaymentCount,
    decimal TotalAmount,
    string ReceiptNumber,
    DateTimeOffset CollectedAt,
    string CollectorBarberName,
    string CollectorBarberStationCode,
    string Message,
    bool ReceiptPrinted = false,
    bool CashDrawerOpened = false,
    string? HardwareFailureMessage = null);
