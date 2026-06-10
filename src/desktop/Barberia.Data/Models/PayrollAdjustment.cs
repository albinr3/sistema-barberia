namespace Barberia.Data.Models;

public sealed record PayrollAdjustment(
    Guid Id,
    Guid PeriodId,
    Guid BarberId,
    long AmountCents,
    string Reason,
    DateTimeOffset CreatedAt);
