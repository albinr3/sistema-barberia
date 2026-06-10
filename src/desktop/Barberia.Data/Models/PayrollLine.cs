namespace Barberia.Data.Models;

public sealed record PayrollLine(
    Guid Id,
    Guid PeriodId,
    Guid BarberId,
    string BarberName,
    int? StationNumber,
    int ClosedServicesCount,
    long CashGeneratedCents,
    long CommissionCents,
    long AdjustmentsCents,
    long TotalCents);
