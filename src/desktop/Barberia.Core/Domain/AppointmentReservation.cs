namespace Barberia.Core.Domain;

public sealed record AppointmentReservation
{
    public AppointmentReservation(
        Guid id,
        Guid barberId,
        AppointmentState state,
        DateTimeOffset scheduledFor,
        TimeSpan protectionWindow)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Appointment id cannot be empty.", nameof(id));
        }

        if (barberId == Guid.Empty)
        {
            throw new ArgumentException("Barber id cannot be empty.", nameof(barberId));
        }

        if (protectionWindow < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(protectionWindow), "Protection window cannot be negative.");
        }

        Id = id;
        BarberId = barberId;
        State = state;
        ScheduledFor = scheduledFor;
        ProtectionWindow = protectionWindow;
    }

    public static TimeSpan DefaultProtectionWindow { get; } = TimeSpan.FromMinutes(15);

    public Guid Id { get; }

    public Guid BarberId { get; }

    public AppointmentState State { get; }

    public DateTimeOffset ScheduledFor { get; }

    public TimeSpan ProtectionWindow { get; }
}
