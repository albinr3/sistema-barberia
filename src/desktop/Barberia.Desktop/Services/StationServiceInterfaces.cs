using Barberia.Data.Models;

namespace Barberia.Desktop.Services;

public interface IKioskStationService
{
    KioskCheckInSnapshot Load();
    KioskCheckInResult RegisterWalkIn(string customerName, bool acceptsAnyBarber, IReadOnlyCollection<Guid> requestedBarberIds);
}

public interface IBarberRotationStationService
{
    BarberCheckInSnapshot Load();
    BarberCheckInResult CheckIn(string stationInput);
    void MarkBarberAvailable(Guid barberId);
    void MarkBarberOffline(Guid barberId);
}

public interface ICashBoxStationService
{
    CashBoxSnapshot Load();
    CashBoxOpeningResult SaveOpeningBalance(decimal openingBalance);
    CashBoxTicketLookupResult LookupTicket(string ticketNumber);
    IReadOnlyList<PendingServicePaymentRow> ListPendingPayments();
    PendingPaymentCollectorLookupResult LookupPendingPaymentCollector(string stationNumberInput);
    PendingServicePaymentResult MarkServicePendingPayment(string ticketNumber, Guid serviceId, decimal additionalAmount);
    PendingPaymentCollectionResult CollectPendingPayments(IReadOnlyCollection<Guid> pendingPaymentIds, CustomerPaymentMethod paymentMethod, string? paymentReference, int collectorStationNumber, decimal tenderedAmount = 0, decimal changeAmount = 0);
    CashBoxDepositResult CloseService(string ticketNumber, Guid serviceId, decimal additionalAmount, CustomerPaymentMethod paymentMethod, string? paymentReference, decimal tenderedAmount = 0, decimal changeAmount = 0);
    void PrintDayReport();
}

