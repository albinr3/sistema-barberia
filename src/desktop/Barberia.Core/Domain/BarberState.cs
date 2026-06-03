namespace Barberia.Core.Domain;

public enum BarberState
{
    NotCheckedIn = 0,
    Available = 1,
    Called = 2,
    InService = 3,
    Offline = 4,
}
