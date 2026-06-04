namespace Barberia.Data.Models;

public sealed record CashPayment(
    Guid Id,
    Guid TurnId,
    Guid BarberId,
    long AmountCents,
    string Currency,
    DateTimeOffset CollectedAt,
    string DeviceId,
    string? ReceiptNumber,
    bool CashDrawerOpened,
    long? CommissionCents)
{
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
            Money.ToCents(amount),
            currency,
            collectedAt,
            deviceId,
            receiptNumber,
            cashDrawerOpened,
            commission.HasValue ? Money.ToCents(commission.Value) : null)
    {
    }
}
