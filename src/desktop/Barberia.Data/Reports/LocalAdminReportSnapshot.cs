namespace Barberia.Data.Reports;

public sealed record LocalAdminReportSnapshot(
    DateTimeOffset From,
    DateTimeOffset To,
    DateTimeOffset GeneratedAt,
    OperationReportSummary Operations,
    CashReportSummary Cash,
    IReadOnlyList<BarberReportRow> Barbers,
    IReadOnlyList<CashPaymentReportRow> RecentPayments);
