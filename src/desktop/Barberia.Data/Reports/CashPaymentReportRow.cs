namespace Barberia.Data.Reports;

public sealed record CashPaymentReportRow(
    Guid PaymentId,
    Guid TurnId,
    string TicketNumber,
    Guid BarberId,
    string BarberName,
    int? BarberStationNumber,
    long AmountCents,
    string Currency,
    DateTimeOffset CollectedAt,
    string DeviceId,
    string? ReceiptNumber,
    bool CashDrawerOpened,
    long? CommissionCents)
{
    public string? BarberStationCode => BarberStationNumber is null ? null : $"B-{BarberStationNumber.Value}";

    public string BarberNameWithStation => BarberStationCode is null ? BarberName : $"{BarberStationCode} - {BarberName}";
}
