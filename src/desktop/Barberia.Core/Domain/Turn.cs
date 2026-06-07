namespace Barberia.Core.Domain;

public sealed record Turn
{
    public Turn(
        Guid id,
        string ticketNumber,
        int displayTicketNumber,
        DateOnly ticketDate,
        TurnState state,
        TurnSource source,
        DateTimeOffset checkedInAt,
        Guid? assignedBarberId = null,
        Guid? appointmentId = null,
        IReadOnlyCollection<Guid>? requestedBarberIds = null,
        string? customerName = null,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? completedAt = null,
        DateTimeOffset? cancelledAt = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Turn id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(ticketNumber))
        {
            throw new ArgumentException("Ticket number is required.", nameof(ticketNumber));
        }

        if (displayTicketNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayTicketNumber), "Display ticket number must be greater than zero.");
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
        DisplayTicketNumber = displayTicketNumber;
        TicketDate = ticketDate;
        State = state;
        Source = source;
        CheckedInAt = checkedInAt;
        AssignedBarberId = assignedBarberId;
        AppointmentId = appointmentId;
        RequestedBarberIds = requestedBarberIds?.Distinct().ToArray();
        CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim();
        StartedAt = startedAt;
        CompletedAt = completedAt;
        CancelledAt = cancelledAt;
    }

    public Guid Id { get; }

    public string TicketNumber { get; }

    public int DisplayTicketNumber { get; }

    public DateOnly TicketDate { get; }

    public TurnState State { get; }

    public TurnSource Source { get; }

    public DateTimeOffset CheckedInAt { get; }

    public Guid? AssignedBarberId { get; }

    public Guid? AppointmentId { get; }

    public IReadOnlyCollection<Guid>? RequestedBarberIds { get; }

    public string? CustomerName { get; }

    public DateTimeOffset? StartedAt { get; }

    public DateTimeOffset? CompletedAt { get; }

    public DateTimeOffset? CancelledAt { get; }
}
