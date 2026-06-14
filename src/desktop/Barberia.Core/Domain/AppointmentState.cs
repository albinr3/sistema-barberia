namespace Barberia.Core.Domain;

public enum AppointmentState
{
    Confirmed = 0,
    ProtectionStarted = 1,
    CheckedIn = 2,
    NoShow = 3,
    Rescheduled = 4,
    Cancelled = 5,
    Completed = 6,
}
