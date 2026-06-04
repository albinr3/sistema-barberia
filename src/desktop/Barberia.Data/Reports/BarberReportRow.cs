namespace Barberia.Data.Reports;

public sealed record BarberReportRow(
    Guid BarberId,
    string DisplayName,
    int ServicesClosed,
    long CashCollectedCents,
    long CommissionCents,
    int PaymentsMissingCommission,
    int CashDrawerOpenCount);
