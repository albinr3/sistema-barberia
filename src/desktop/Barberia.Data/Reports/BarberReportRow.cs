namespace Barberia.Data.Reports;

public sealed record BarberReportRow(
    Guid BarberId,
    string DisplayName,
    int? StationNumber,
    int ServicesClosed,
    long CashCollectedCents,
    long CommissionCents,
    int PaymentsMissingCommission,
    int CashDrawerOpenCount)
{
    public string? StationCode => StationNumber is null ? null : $"B-{StationNumber.Value}";

    public string DisplayNameWithStation => StationCode is null ? DisplayName : $"{StationCode} - {DisplayName}";
}
