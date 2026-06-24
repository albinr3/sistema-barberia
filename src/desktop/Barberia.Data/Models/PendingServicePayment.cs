namespace Barberia.Data.Models;

public sealed record PendingServicePayment(
    Guid Id,
    Guid TurnId,
    Guid BarberId,
    Guid ServiceId,
    DateOnly BusinessDate,
    long ServicePriceCents,
    long AdditionalCents,
    long AmountCents,
    long CommissionCents,
    string Currency,
    string DeviceId,
    DateTimeOffset PendingAt,
    DateTimeOffset? PaidAt = null,
    DateTimeOffset? VoidedAt = null,
    string? ReceiptNumber = null,
    CustomerPaymentMethod? PaymentMethod = null,
    string? PaymentReference = null)
{
    public PendingServicePayment(
        Guid id,
        Guid turnId,
        Guid barberId,
        Guid serviceId,
        DateOnly businessDate,
        decimal servicePrice,
        decimal additional,
        decimal amount,
        decimal commission,
        string currency,
        string deviceId,
        DateTimeOffset pendingAt)
        : this(
            id,
            turnId,
            barberId,
            serviceId,
            businessDate,
            Money.ToCents(servicePrice),
            Money.ToCents(additional),
            Money.ToCents(amount),
            Money.ToCents(commission),
            currency,
            deviceId,
            pendingAt)
    {
    }
}
