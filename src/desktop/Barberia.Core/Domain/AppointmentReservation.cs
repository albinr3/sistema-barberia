namespace Barberia.Core.Domain;

public sealed record AppointmentReservation
{
    public AppointmentReservation(
        Guid id,
        Guid barberId,
        AppointmentState state,
        DateTimeOffset scheduledFor,
        TimeSpan protectionWindow,
        Guid? serviceId = null,
        DateTimeOffset? endsAt = null,
        string? appointmentCode = null,
        string? customerName = null,
        DateTimeOffset? checkedInAt = null,
        DateTimeOffset? noShowAt = null,
        DateTimeOffset? completedAt = null)
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

        if (serviceId == Guid.Empty)
        {
            throw new ArgumentException("Service id cannot be empty.", nameof(serviceId));
        }

        Id = id;
        BarberId = barberId;
        State = state;
        ScheduledFor = scheduledFor;
        EndsAt = endsAt ?? scheduledFor;
        ProtectionWindow = protectionWindow;
        ServiceId = serviceId;
        AppointmentCode = string.IsNullOrWhiteSpace(appointmentCode)
            ? null
            : appointmentCode.Trim().ToUpperInvariant();
        CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim();
        CheckedInAt = checkedInAt;
        NoShowAt = noShowAt;
        CompletedAt = completedAt;
    }

    public static TimeSpan DefaultProtectionWindow { get; } = TimeSpan.FromMinutes(15);

    public Guid Id { get; }

    public Guid BarberId { get; }

    public AppointmentState State { get; }

    public DateTimeOffset ScheduledFor { get; }

    public DateTimeOffset EndsAt { get; }

    public TimeSpan ProtectionWindow { get; }

    public Guid? ServiceId { get; }

    public string? AppointmentCode { get; }

    public string? CustomerName { get; }

    public DateTimeOffset? CheckedInAt { get; }

    public DateTimeOffset? NoShowAt { get; }

    public DateTimeOffset? CompletedAt { get; }
}
