namespace Barberia.Hardware.Pos;

public sealed record BarberDayReport(
    string BarberName,
    int ServicesClosed,
    decimal CashCollected
);

public sealed record DayReportPrintJob(
    decimal TotalSales,
    decimal OpeningCash,
    decimal CashCollected,
    decimal ZelleCollected,
    decimal CashInDrawer,
    IReadOnlyList<BarberDayReport> Barbers,
    DateTimeOffset GeneratedAt,
    string DeviceId
);
