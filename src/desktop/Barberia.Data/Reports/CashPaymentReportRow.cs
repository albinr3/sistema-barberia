namespace Barberia.Data.Reports;

public sealed record CashPaymentReportRow(
    Guid PaymentId,
    Guid TurnId,
    string TicketNumber,
    Guid BarberId,
    string BarberName,
    long AmountCents,
    string Currency,
    DateTimeOffset CollectedAt,
    string DeviceId,
    string? ReceiptNumber,
    bool CashDrawerOpened,
    long? CommissionCents);
