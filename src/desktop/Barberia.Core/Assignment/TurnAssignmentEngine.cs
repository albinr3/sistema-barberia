using Barberia.Core.Domain;

namespace Barberia.Core.Assignment;

public sealed class TurnAssignmentEngine
{
    public TurnAssignmentDecision AssignNextTurn(TurnAssignmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var turn = request.Turns
            .Where(candidate => candidate.State == TurnState.Waiting)
            .OrderBy(candidate => candidate.CheckedInAt)
            .ThenBy(candidate => candidate.TicketNumber, StringComparer.Ordinal)
            .FirstOrDefault();

        if (turn is null)
        {
            throw new InvalidOperationException("No waiting turns are available for assignment.");
        }

        var requestedBarberIds = turn.RequestedBarberIds?.ToHashSet() ?? [];
        var requestedAnyBarber = requestedBarberIds.Count == 0;
        var protectedBarberIds = GetProtectedBarberIds(request.Appointments, request.Now);

        var compatibleBarbers = request.Barbers
            .Where(barber => requestedAnyBarber || requestedBarberIds.Contains(barber.Id))
            .Where(barber => barber.State == BarberState.Available)
            .Where(barber => !protectedBarberIds.Contains(barber.Id))
            .ToArray();

        if (compatibleBarbers.Length == 0)
        {
            throw new InvalidOperationException("No compatible available barbers are eligible for assignment.");
        }

        var zeroClientBarbers = compatibleBarbers
            .Where(barber => barber.ClientsServedToday == 0)
            .ToArray();

        var selectedBarber = zeroClientBarbers.Length > 0
            ? SelectByInitialQueue(zeroClientBarbers, request.RotationQueue)
            : SelectByRotatingQueue(compatibleBarbers, request.RotationQueue);

        return new TurnAssignmentDecision(
            turn.Id,
            turn.TicketNumber,
            turn.DisplayTicketNumber,
            selectedBarber.Id,
            TurnState.Called,
            BarberState.Called);
    }

    public CashBoxCloseResult CloseServiceAtCashBox(CashBoxCloseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rotationQueue = request.RotationQueue
            .Where(barberId => barberId != request.BarberId)
            .ToList();

        rotationQueue.Add(request.BarberId);

        return new CashBoxCloseResult(request.BarberId, BarberState.Available, rotationQueue);
    }

    private static HashSet<Guid> GetProtectedBarberIds(IEnumerable<AppointmentReservation> appointments, DateTimeOffset now)
    {
        return appointments
            .Where(appointment => appointment.State is AppointmentState.Confirmed or AppointmentState.ProtectionStarted)
            .Where(appointment => IsWithinProtectionWindow(appointment, now))
            .Select(appointment => appointment.BarberId)
            .ToHashSet();
    }

    private static bool IsWithinProtectionWindow(AppointmentReservation appointment, DateTimeOffset now)
    {
        var protectionStartsAt = appointment.ScheduledFor - appointment.ProtectionWindow;

        return now >= protectionStartsAt && now <= appointment.ScheduledFor;
    }

    private static Barber SelectByInitialQueue(IEnumerable<Barber> barbers, IReadOnlyList<Guid> rotationQueue)
    {
        return barbers
            .OrderBy(barber => QueueIndexOrMax(barber.Id, rotationQueue))
            .ThenBy(barber => barber.RotationOrder)
            .ThenBy(barber => barber.CheckedInAt)
            .ThenBy(barber => barber.Id)
            .First();
    }

    private static Barber SelectByRotatingQueue(IEnumerable<Barber> barbers, IReadOnlyList<Guid> rotationQueue)
    {
        return barbers
            .OrderBy(barber => QueueIndexOrMax(barber.Id, rotationQueue))
            .ThenBy(barber => barber.RotationOrder)
            .ThenBy(barber => barber.Id)
            .First();
    }

    private static int QueueIndexOrMax(Guid barberId, IReadOnlyList<Guid> rotationQueue)
    {
        for (var index = 0; index < rotationQueue.Count; index++)
        {
            if (rotationQueue[index] == barberId)
            {
                return index;
            }
        }

        return int.MaxValue;
    }
}
