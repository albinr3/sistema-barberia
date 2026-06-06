namespace Barberia.Core.Domain;

public enum TurnState
{
    Waiting = 0,
    Called = 2,
    InService = 3,
    Completed = 4,
    Cancelled = 5,
    NoShow = 6,
    Voided = 7,
}
