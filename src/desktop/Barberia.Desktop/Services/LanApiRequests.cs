using Barberia.Data.Models;

namespace Barberia.Desktop.Services;

internal sealed record LanKioskWalkInRequest(
    string? CustomerName,
    bool AcceptsAnyBarber,
    IReadOnlyList<Guid>? RequestedBarberIds);

internal sealed record LanStationInputRequest(string StationInput);

internal sealed record LanBarberStateRequest(Guid BarberId);

internal sealed record LanTicketInputRequest(string TicketNumber);

internal sealed record LanStartServiceRequest(string StationInput, string ScannedTicketNumber);

internal sealed record LanCashBoxOpeningRequest(decimal OpeningBalance);

internal sealed record LanCashBoxCloseServiceRequest(
    string TicketNumber,
    Guid ServiceId,
    decimal AdditionalAmount,
    CustomerPaymentMethod PaymentMethod,
    string? PaymentReference,
    decimal TenderedAmount,
    decimal ChangeAmount);

internal sealed record LanPendingPaymentRequest(
    string TicketNumber,
    Guid ServiceId,
    decimal AdditionalAmount);

internal sealed record LanPendingPaymentCollectorRequest(string StationNumberInput);

internal sealed record LanCollectPendingPaymentsRequest(
    IReadOnlyList<Guid> PendingPaymentIds,
    CustomerPaymentMethod PaymentMethod,
    string? PaymentReference,
    int CollectorStationNumber,
    decimal TenderedAmount,
    decimal ChangeAmount);

internal sealed record LanHardwareEventRequest(
    string StationRole,
    string DeviceId,
    string EventType,
    bool Succeeded,
    string? Message,
    string? ReceiptNumber,
    int? DisplayTicketNumber);

