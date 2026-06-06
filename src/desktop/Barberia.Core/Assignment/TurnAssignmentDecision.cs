using Barberia.Core.Domain;

namespace Barberia.Core.Assignment;

public sealed record TurnAssignmentDecision(
    Guid TurnId,
    string TicketNumber,
    int DisplayTicketNumber,
    Guid BarberId,
    TurnState TurnState,
    BarberState BarberState);
