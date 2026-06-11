using Barberia.Data.Models;

namespace Barberia.Data.Reports;

public sealed record CashPaymentReportRow(
    Guid PaymentId,
    Guid TurnId,
    int DisplayTicketNumber,
    string InternalTicketNumber,
    Guid BarberId,
    string BarberName,
    int? BarberStationNumber,
    string? ServiceName,
    long? ServicePriceCents,
    long AdditionalCents,
    long AmountCents,
    string Currency,
    DateTimeOffset CollectedAt,
    string DeviceId,
    string? ReceiptNumber,
    bool CashDrawerOpened,
    long? CommissionCents,
    CustomerPaymentMethod PaymentMethod,
    string? PaymentReference)
{
    public string? BarberStationCode => BarberStationNumber is null ? null : $"B-{BarberStationNumber.Value}";

    public string BarberNameWithStation => BarberStationCode is null ? BarberName : $"{BarberStationCode} - {BarberName}";

    public string ServiceSummary
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(ServiceName) ? "Service not registered" : ServiceName;
            if (ServicePriceCents is null)
            {
                return AdditionalCents > 0 ? $"{name} - additional {AdditionalCents / 100m:0.00}" : name;
            }

            return AdditionalCents > 0
                ? $"{name} {ServicePriceCents.Value / 100m:0.00} + additional {AdditionalCents / 100m:0.00}"
                : $"{name} {ServicePriceCents.Value / 100m:0.00}";
        }
    }
}
