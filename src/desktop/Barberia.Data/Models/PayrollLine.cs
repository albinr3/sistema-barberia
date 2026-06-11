namespace Barberia.Data.Models;

public sealed record PayrollLine(
    Guid Id,
    Guid PeriodId,
    Guid BarberId,
    string BarberName,
    string? BarberImagePath,
    int? StationNumber,
    int ClosedServicesCount,
    long SalesGeneratedCents,
    long CommissionCents,
    long AdjustmentsCents,
    long TotalCents);
