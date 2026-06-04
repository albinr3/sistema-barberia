namespace Barberia.Data.Reports;

public sealed record CashReportSummary(
    int PaymentCount,
    long TotalAmountCents,
    long CommissionCents,
    int PaymentsMissingCommission,
    int CashDrawerOpenCount,
    string Currency);
