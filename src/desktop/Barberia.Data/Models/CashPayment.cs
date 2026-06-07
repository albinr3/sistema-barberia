namespace Barberia.Data.Models;

public sealed record CashPayment(
    Guid Id,
    Guid TurnId,
    Guid BarberId,
    Guid? ServiceId,
    long AmountCents,
    string Currency,
    DateTimeOffset CollectedAt,
    string DeviceId,
    string? ReceiptNumber,
    bool CashDrawerOpened,
    long? CommissionCents,
    long? ServicePriceCents,
    long AdditionalCents)
{
    public CashPayment(
        Guid id,
        Guid turnId,
        Guid barberId,
        long amountCents,
        string currency,
        DateTimeOffset collectedAt,
        string deviceId,
        string? receiptNumber,
        bool cashDrawerOpened,
        long? commissionCents)
        : this(
            id,
            turnId,
            barberId,
            null,
            amountCents,
            currency,
            collectedAt,
            deviceId,
            receiptNumber,
            cashDrawerOpened,
            commissionCents,
            null,
            0)
    {
    }

    public CashPayment(
        Guid id,
        Guid turnId,
        Guid barberId,
        decimal amount,
        string currency,
        DateTimeOffset collectedAt,
        string deviceId,
        string? receiptNumber,
        bool cashDrawerOpened,
        decimal? commission)
        : this(
            id,
            turnId,
            barberId,
            null,
            amount,
            currency,
            collectedAt,
            deviceId,
            receiptNumber,
            cashDrawerOpened,
            commission,
            null,
            0)
    {
    }

    public CashPayment(
        Guid id,
        Guid turnId,
        Guid barberId,
        Guid? serviceId,
        decimal amount,
        string currency,
        DateTimeOffset collectedAt,
        string deviceId,
        string? receiptNumber,
        bool cashDrawerOpened,
        decimal? commission,
        decimal? servicePrice,
        decimal additional)
        : this(
            id,
            turnId,
            barberId,
            serviceId,
            Money.ToCents(amount),
            currency,
            collectedAt,
            deviceId,
            receiptNumber,
            cashDrawerOpened,
            commission.HasValue ? Money.ToCents(commission.Value) : null,
            servicePrice.HasValue ? Money.ToCents(servicePrice.Value) : null,
            Money.ToCents(additional))
    {
    }
}
