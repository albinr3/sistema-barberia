using Barberia.Core.Domain;

namespace Barberia.Data.Models;

public sealed record TicketHistoryRow(
    string InternalTicketNumber,
    int DisplayTicketNumber,
    string? CustomerName,
    TurnSource Source,
    TurnState FinalState,
    string? AssignedBarberName,
    string? AssignedBarberImagePath,
    DateTimeOffset CheckedInAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? ChargedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    string? ServiceName,
    decimal? Amount,
    string? ReceiptNumber,
    CustomerPaymentMethod? PaymentMethod,
    string? PaymentReference,
    string? PaymentResultText);
