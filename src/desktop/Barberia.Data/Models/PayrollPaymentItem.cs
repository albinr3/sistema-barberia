namespace Barberia.Data.Models;

public sealed record PayrollPaymentItem(
    Guid Id,
    Guid PeriodId,
    Guid BarberId,
    Guid PaymentId);
