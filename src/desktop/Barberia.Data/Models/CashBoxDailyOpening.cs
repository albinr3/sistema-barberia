namespace Barberia.Data.Models;

public sealed record CashBoxDailyOpening(
    DateOnly BusinessDate,
    long OpeningBalanceCents,
    string Currency,
    DateTimeOffset OpenedAt,
    string OpenedDeviceId,
    DateTimeOffset UpdatedAt,
    string UpdatedDeviceId)
{
    public CashBoxDailyOpening(
        DateOnly businessDate,
        decimal openingBalance,
        string currency,
        DateTimeOffset openedAt,
        string openedDeviceId,
        DateTimeOffset updatedAt,
        string updatedDeviceId)
        : this(
            businessDate,
            Money.ToCents(openingBalance),
            currency,
            openedAt,
            openedDeviceId,
            updatedAt,
            updatedDeviceId)
    {
    }
}
