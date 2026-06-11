namespace Barberia.Hardware.Pos;

public sealed record BarberDayReport(
    string BarberName,
    int ServicesClosed,
    decimal CashCollected
);

public sealed record DayReportPrintJob(
    decimal TotalCash,
    IReadOnlyList<BarberDayReport> Barbers,
    DateTimeOffset GeneratedAt,
    string DeviceId
);
