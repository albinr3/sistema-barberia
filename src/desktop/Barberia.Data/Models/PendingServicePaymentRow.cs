namespace Barberia.Data.Models;

public sealed record PendingServicePaymentRow(
    Guid Id,
    Guid TurnId,
    Guid BarberId,
    Guid ServiceId,
    DateOnly BusinessDate,
    int DisplayTicketNumber,
    string InternalTicketNumber,
    string CustomerName,
    string BarberName,
    string BarberStationCode,
    string ServiceName,
    long ServicePriceCents,
    long AdditionalCents,
    long AmountCents,
    long CommissionCents,
    string Currency,
    DateTimeOffset PendingAt)
{
    public decimal ServicePrice => Money.FromCents(ServicePriceCents);

    public decimal AdditionalAmount => Money.FromCents(AdditionalCents);

    public decimal Amount => Money.FromCents(AmountCents);

    public decimal Commission => Money.FromCents(CommissionCents);
}
