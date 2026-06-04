using Barberia.Core.Assignment;
using Barberia.Core.Domain;
using Xunit;

namespace Barberia.Core.Tests;

public sealed class TurnAssignmentEngineTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 6, 3, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AssignNextTurn_SelectsSpecificRequestedBarber()
    {
        var requestedBarber = Guid.NewGuid();
        var otherBarber = Guid.NewGuid();
        var request = CreateRequest(
            [WaitingTurn("A-001", BaseTime, [requestedBarber])],
            [AvailableBarber(otherBarber, clientsServedToday: 1, rotationOrder: 0), AvailableBarber(requestedBarber, clientsServedToday: 1, rotationOrder: 1)],
            [otherBarber, requestedBarber]);

        var decision = new TurnAssignmentEngine().AssignNextTurn(request);

        Assert.Equal(requestedBarber, decision.BarberId);
        Assert.Equal(TurnState.Assigned, decision.TurnState);
        Assert.Equal(BarberState.Called, decision.BarberState);
    }

    [Fact]
    public void AssignNextTurn_SelectsOnlyFromRequestedBarberSet()
    {
        var firstInQueue = Guid.NewGuid();
        var requestedLater = Guid.NewGuid();
        var requestedEarlier = Guid.NewGuid();
        var request = CreateRequest(
            [WaitingTurn("A-001", BaseTime, [requestedLater, requestedEarlier])],
            [
                AvailableBarber(firstInQueue, clientsServedToday: 1, rotationOrder: 0),
                AvailableBarber(requestedEarlier, clientsServedToday: 1, rotationOrder: 1),
                AvailableBarber(requestedLater, clientsServedToday: 1, rotationOrder: 2),
            ],
            [firstInQueue, requestedEarlier, requestedLater]);

        var decision = new TurnAssignmentEngine().AssignNextTurn(request);

        Assert.Equal(requestedEarlier, decision.BarberId);
    }

    [Fact]
    public void AssignNextTurn_WhenAnyBarberRequested_UsesCompatibleAvailableQueue()
    {
        var firstAvailable = Guid.NewGuid();
        var secondAvailable = Guid.NewGuid();
        var request = CreateRequest(
            [WaitingTurn("A-001", BaseTime)],
            [AvailableBarber(firstAvailable, clientsServedToday: 1, rotationOrder: 0), AvailableBarber(secondAvailable, clientsServedToday: 1, rotationOrder: 1)],
            [firstAvailable, secondAvailable]);

        var decision = new TurnAssignmentEngine().AssignNextTurn(request);

        Assert.Equal(firstAvailable, decision.BarberId);
    }

    [Fact]
    public void AssignNextTurn_PrioritizesCompatibleBarbersWithZeroClientsServedToday()
    {
        var servedBarber = Guid.NewGuid();
        var zeroClientBarber = Guid.NewGuid();
        var request = CreateRequest(
            [WaitingTurn("A-001", BaseTime)],
            [AvailableBarber(servedBarber, clientsServedToday: 1, rotationOrder: 0), AvailableBarber(zeroClientBarber, clientsServedToday: 0, rotationOrder: 1)],
            [servedBarber, zeroClientBarber]);

        var decision = new TurnAssignmentEngine().AssignNextTurn(request);

        Assert.Equal(zeroClientBarber, decision.BarberId);
    }

    [Fact]
    public void AssignNextTurn_TiebreaksZeroClientBarbersByInitialQueueOrder()
    {
        var firstZeroClient = Guid.NewGuid();
        var secondZeroClient = Guid.NewGuid();
        var request = CreateRequest(
            [WaitingTurn("A-001", BaseTime)],
            [AvailableBarber(secondZeroClient, clientsServedToday: 0, rotationOrder: 1), AvailableBarber(firstZeroClient, clientsServedToday: 0, rotationOrder: 0)],
            [firstZeroClient, secondZeroClient]);

        var decision = new TurnAssignmentEngine().AssignNextTurn(request);

        Assert.Equal(firstZeroClient, decision.BarberId);
    }

    [Fact]
    public void AssignNextTurn_UsesRotatingQueueAfterAllCompatibleBarbersHaveServed()
    {
        var firstInRotation = Guid.NewGuid();
        var secondInRotation = Guid.NewGuid();
        var request = CreateRequest(
            [WaitingTurn("A-001", BaseTime)],
            [AvailableBarber(secondInRotation, clientsServedToday: 3, rotationOrder: 1), AvailableBarber(firstInRotation, clientsServedToday: 2, rotationOrder: 0)],
            [firstInRotation, secondInRotation]);

        var decision = new TurnAssignmentEngine().AssignNextTurn(request);

        Assert.Equal(firstInRotation, decision.BarberId);
    }

    [Fact]
    public void AssignNextTurn_DoesNotPreferLowerTotalClientsAfterFirstClient()
    {
        var firstInRotationWithMoreClients = Guid.NewGuid();
        var laterWithFewerClients = Guid.NewGuid();
        var request = CreateRequest(
            [WaitingTurn("A-001", BaseTime)],
            [AvailableBarber(laterWithFewerClients, clientsServedToday: 1, rotationOrder: 1), AvailableBarber(firstInRotationWithMoreClients, clientsServedToday: 5, rotationOrder: 0)],
            [firstInRotationWithMoreClients, laterWithFewerClients]);

        var decision = new TurnAssignmentEngine().AssignNextTurn(request);

        Assert.Equal(firstInRotationWithMoreClients, decision.BarberId);
    }

    [Theory]
    [InlineData(BarberState.NotCheckedIn)]
    [InlineData(BarberState.Called)]
    [InlineData(BarberState.InService)]
    [InlineData(BarberState.Offline)]
    public void AssignNextTurn_ExcludesBarbersThatAreNotAvailable(BarberState unavailableState)
    {
        var unavailableBarber = Guid.NewGuid();
        var availableBarber = Guid.NewGuid();
        var request = CreateRequest(
            [WaitingTurn("A-001", BaseTime)],
            [BarberWithState(unavailableBarber, unavailableState, rotationOrder: 0), AvailableBarber(availableBarber, clientsServedToday: 1, rotationOrder: 1)],
            [unavailableBarber, availableBarber]);

        var decision = new TurnAssignmentEngine().AssignNextTurn(request);

        Assert.Equal(availableBarber, decision.BarberId);
    }

    [Fact]
    public void CloseServiceAtCashBox_MovesBarberToEndOfRotatingQueue()
    {
        var closingBarber = Guid.NewGuid();
        var nextBarber = Guid.NewGuid();
        var lastBarber = Guid.NewGuid();

        var result = new TurnAssignmentEngine().CloseServiceAtCashBox(new CashBoxCloseRequest(closingBarber, [closingBarber, nextBarber, lastBarber]));

        Assert.Equal(BarberState.Available, result.BarberState);
        Assert.Equal([nextBarber, lastBarber, closingBarber], result.RotationQueue);
    }

    [Fact]
    public void AssignNextTurn_BlocksBarberWithConfirmedUpcomingAppointment()
    {
        var protectedBarber = Guid.NewGuid();
        var availableBarber = Guid.NewGuid();
        var request = CreateRequest(
            [WaitingTurn("A-001", BaseTime)],
            [AvailableBarber(protectedBarber, clientsServedToday: 1, rotationOrder: 0), AvailableBarber(availableBarber, clientsServedToday: 1, rotationOrder: 1)],
            [protectedBarber, availableBarber],
            [Appointment(protectedBarber, AppointmentState.Confirmed, BaseTime.AddMinutes(10))]);

        var decision = new TurnAssignmentEngine().AssignNextTurn(request);

        Assert.Equal(availableBarber, decision.BarberId);
    }

    [Fact]
    public void AssignNextTurn_UsesOldestWaitingTurnAsInitialAssignmentCandidate()
    {
        var barberForOldestTurn = Guid.NewGuid();
        var barberForNewerTurn = Guid.NewGuid();
        var oldestTurn = WaitingTurn("A-001", BaseTime, [barberForOldestTurn]);
        var newerTurn = WaitingTurn("A-002", BaseTime.AddMinutes(5), [barberForNewerTurn]);
        var request = CreateRequest(
            [newerTurn, oldestTurn],
            [AvailableBarber(barberForNewerTurn, clientsServedToday: 1, rotationOrder: 0), AvailableBarber(barberForOldestTurn, clientsServedToday: 1, rotationOrder: 1)],
            [barberForNewerTurn, barberForOldestTurn]);

        var decision = new TurnAssignmentEngine().AssignNextTurn(request);

        Assert.Equal(oldestTurn.Id, decision.TurnId);
        Assert.Equal("A-001", decision.TicketNumber);
        Assert.Equal(barberForOldestTurn, decision.BarberId);
    }

    private static TurnAssignmentRequest CreateRequest(
        IEnumerable<Turn> turns,
        IEnumerable<Barber> barbers,
        IEnumerable<Guid> rotationQueue,
        IEnumerable<AppointmentReservation>? appointments = null)
    {
        return new TurnAssignmentRequest(turns, barbers, rotationQueue, BaseTime, appointments);
    }

    private static Turn WaitingTurn(string ticketNumber, DateTimeOffset checkedInAt, IReadOnlyCollection<Guid>? requestedBarberIds = null)
    {
        return new Turn(Guid.NewGuid(), ticketNumber, TurnState.Waiting, TurnSource.WalkIn, checkedInAt, requestedBarberIds: requestedBarberIds);
    }

    private static Barber AvailableBarber(Guid id, int clientsServedToday, int rotationOrder)
    {
        return new Barber(id, $"Barber {id:N}", BarberState.Available, clientsServedToday, rotationOrder, BaseTime.AddMinutes(rotationOrder));
    }

    private static Barber BarberWithState(Guid id, BarberState state, int rotationOrder)
    {
        return new Barber(id, $"Barber {id:N}", state, clientsServedToday: 0, rotationOrder, BaseTime.AddMinutes(rotationOrder));
    }

    private static AppointmentReservation Appointment(Guid barberId, AppointmentState state, DateTimeOffset scheduledFor)
    {
        return new AppointmentReservation(Guid.NewGuid(), barberId, state, scheduledFor, AppointmentReservation.DefaultProtectionWindow);
    }
}
