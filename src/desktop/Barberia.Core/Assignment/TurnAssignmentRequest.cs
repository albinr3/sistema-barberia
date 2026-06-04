using Barberia.Core.Domain;

namespace Barberia.Core.Assignment;

public sealed record TurnAssignmentRequest
{
    public TurnAssignmentRequest(
        IEnumerable<Turn> turns,
        IEnumerable<Barber> barbers,
        IEnumerable<Guid> rotationQueue,
        DateTimeOffset now,
        IEnumerable<AppointmentReservation>? appointments = null)
    {
        Turns = turns?.ToArray() ?? throw new ArgumentNullException(nameof(turns));
        Barbers = barbers?.ToArray() ?? throw new ArgumentNullException(nameof(barbers));
        RotationQueue = rotationQueue?.ToArray() ?? throw new ArgumentNullException(nameof(rotationQueue));
        Now = now;
        Appointments = appointments?.ToArray() ?? [];
    }

    public IReadOnlyCollection<Turn> Turns { get; }

    public IReadOnlyCollection<Barber> Barbers { get; }

    public IReadOnlyList<Guid> RotationQueue { get; }

    public DateTimeOffset Now { get; }

    public IReadOnlyCollection<AppointmentReservation> Appointments { get; }
}
