namespace Barberia.Core.Domain;

public sealed record Turn
{
    public Turn(
        Guid id,
        string ticketNumber,
        TurnState state,
        TurnSource source,
        DateTimeOffset checkedInAt,
        Guid? assignedBarberId = null,
        Guid? appointmentId = null,
        IReadOnlyCollection<Guid>? requestedBarberIds = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Turn id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(ticketNumber))
        {
            throw new ArgumentException("Ticket number is required.", nameof(ticketNumber));
        }

        if (assignedBarberId == Guid.Empty)
        {
            throw new ArgumentException("Assigned barber id cannot be empty.", nameof(assignedBarberId));
        }

        if (appointmentId == Guid.Empty)
        {
            throw new ArgumentException("Appointment id cannot be empty.", nameof(appointmentId));
        }

        if (requestedBarberIds is not null && requestedBarberIds.Any(requestedId => requestedId == Guid.Empty))
        {
            throw new ArgumentException("Requested barber ids cannot contain empty values.", nameof(requestedBarberIds));
        }

        Id = id;
        TicketNumber = ticketNumber.Trim();
        State = state;
        Source = source;
        CheckedInAt = checkedInAt;
        AssignedBarberId = assignedBarberId;
        AppointmentId = appointmentId;
        RequestedBarberIds = requestedBarberIds?.Distinct().ToArray();
    }

    public Guid Id { get; }

    public string TicketNumber { get; }

    public TurnState State { get; }

    public TurnSource Source { get; }

    public DateTimeOffset CheckedInAt { get; }

    public Guid? AssignedBarberId { get; }

    public Guid? AppointmentId { get; }

    public IReadOnlyCollection<Guid>? RequestedBarberIds { get; }
}
