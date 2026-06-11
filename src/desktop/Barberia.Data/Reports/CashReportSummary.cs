namespace Barberia.Data.Reports;

public sealed record CashReportSummary(
    int TotalPaymentCount,
    long TotalSalesCents,
    long CashSalesCents,
    long ZelleSalesCents,
    int CashPaymentCount,
    int ZellePaymentCount,
    long CommissionCents,
    int PaymentsMissingCommission,
    int CashDrawerOpenCount,
    string Currency);
