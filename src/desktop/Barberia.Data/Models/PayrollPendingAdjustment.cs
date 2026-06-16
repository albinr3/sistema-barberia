namespace Barberia.Data.Models;

public sealed record PayrollPendingAdjustment(
    Guid Id,
    Guid CommandId,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    Guid BarberId,
    long AmountCents,
    string Reason,
    DateTimeOffset CreatedAt)
{
    public PayrollAdjustment ToAdjustment(Guid periodId)
    {
        return new PayrollAdjustment(Id, periodId, BarberId, AmountCents, Reason, CreatedAt);
    }
}
