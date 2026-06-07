namespace Barberia.Desktop.Services;

public sealed record CashBoxTicketLookupResult(
    int DisplayTicketNumber,
    string InternalTicketNumber,
    string CustomerName,
    string BarberName,
    string BarberStationCode);
