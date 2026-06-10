namespace Barberia.Data.Models;

public sealed record PayrollPeriod(
    Guid Id,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    PayrollPeriodState State,
    long TotalServices,
    long TotalCommissionCents,
    long TotalAdjustmentsCents,
    long TotalToPayCents,
    PayrollPaymentMethod? PaymentMethod,
    string? PaymentReference,
    string? Notes,
    DateTimeOffset GeneratedAt,
    DateTimeOffset? PaidAt);
