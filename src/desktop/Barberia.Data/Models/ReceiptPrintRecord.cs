namespace Barberia.Data.Models;

public sealed record ReceiptPrintRecord(
    Guid PaymentId,
    string? ReceiptNumber,
    int DisplayTicketNumber,
    string InternalTicketNumber,
    string BarberName,
    string BarberStationCode,
    string ServiceName,
    decimal ServicePrice,
    decimal AdditionalAmount,
    decimal TotalAmount,
    decimal Commission,
    string Currency,
    DateTimeOffset CollectedAt,
    string DeviceId,
    CustomerPaymentMethod PaymentMethod);
