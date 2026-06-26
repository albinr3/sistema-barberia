using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Repositories;
using Barberia.Data.Sync;
using Barberia.Desktop.Services;
using Xunit;

namespace Barberia.Desktop.Tests.Services;

public sealed class BarberPanelServiceTests
{
    [Fact]
    public void StartService_WithMatchingStation_StartsCalledTicket()
    {
        using var database = TestDatabase.Create();
        var now = OperationalClock.Now;
        var barberId = Guid.NewGuid();
        var turnId = Guid.NewGuid();

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            var barberRepository = new LocalBarberRepository(connection);
            var turnRepository = new LocalTurnRepository(connection);
            barberRepository.Upsert(new Barber(barberId, "Ana", BarberState.Called, 0, 0, now.AddMinutes(-10), stationNumber: 1), now);
            turnRepository.Upsert(CreateTurn(turnId, "W202606261000001", 77, TurnState.Called, now.AddMinutes(-5), barberId), now);
        }

        var result = new BarberPanelService(database.ConnectionFactory).StartService("b-1", "77");

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var savedBarber = new LocalBarberRepository(verifyConnection).GetById(barberId);
        var savedTurn = new LocalTurnRepository(verifyConnection).GetById(turnId);
        var outboxEvents = new SyncOutboxRepository(verifyConnection).ListAll();

        Assert.Equal(barberId, result.BarberId);
        Assert.Equal(77, result.DisplayTicketNumber);
        Assert.Equal("B-1", result.BarberStationCode);
        Assert.Equal(BarberState.InService, savedBarber?.State);
        Assert.Equal(TurnState.InService, savedTurn?.State);
        Assert.NotNull(savedTurn?.StartedAt);
        Assert.Contains(outboxEvents, outboxEvent => outboxEvent.EventType == "ticket.started" && outboxEvent.AggregateId == turnId);
    }

    [Fact]
    public void StartService_WithDifferentAvailableStation_ReassignsAndStartsTicket()
    {
        using var database = TestDatabase.Create();
        var now = OperationalClock.Now;
        var assignedBarberId = Guid.NewGuid();
        var scannedBarberId = Guid.NewGuid();
        var turnId = Guid.NewGuid();

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            var barberRepository = new LocalBarberRepository(connection);
            var turnRepository = new LocalTurnRepository(connection);
            barberRepository.Upsert(new Barber(assignedBarberId, "Ana", BarberState.Called, 0, 0, now.AddMinutes(-10), stationNumber: 1), now);
            barberRepository.Upsert(new Barber(scannedBarberId, "Luis", BarberState.Available, 0, 1, now.AddMinutes(-8), stationNumber: 2), now);
            turnRepository.Upsert(CreateTurn(turnId, "W202606261000002", 78, TurnState.Called, now.AddMinutes(-5), assignedBarberId), now);
        }

        var result = new BarberPanelService(database.ConnectionFactory).StartService("B-2", "78");

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var barberRepositoryVerify = new LocalBarberRepository(verifyConnection);
        var savedAssignedBarber = barberRepositoryVerify.GetById(assignedBarberId);
        var savedScannedBarber = barberRepositoryVerify.GetById(scannedBarberId);
        var savedTurn = new LocalTurnRepository(verifyConnection).GetById(turnId);
        var outboxEvents = new SyncOutboxRepository(verifyConnection).ListAll();

        Assert.Equal(BarberPanelStartOutcome.Started, result.Outcome);
        Assert.Equal(scannedBarberId, result.BarberId);
        Assert.Equal("B-2", result.BarberStationCode);
        Assert.Equal(BarberState.Available, savedAssignedBarber?.State);
        Assert.Equal(BarberState.InService, savedScannedBarber?.State);
        Assert.Equal(TurnState.InService, savedTurn?.State);
        Assert.Equal(scannedBarberId, savedTurn?.AssignedBarberId);
        Assert.Equal([scannedBarberId], savedTurn?.RequestedBarberIds);
        Assert.NotNull(savedTurn?.StartedAt);
        Assert.Contains(outboxEvents, outboxEvent => outboxEvent.EventType == "ticket.called" && outboxEvent.AggregateId == turnId);
        Assert.Contains(outboxEvents, outboxEvent => outboxEvent.EventType == "ticket.started" && outboxEvent.AggregateId == turnId);
        Assert.Contains(outboxEvents, outboxEvent => outboxEvent.EventType == "ticket.auto_reassigned" && outboxEvent.AggregateId == turnId);
    }

    [Theory]
    [InlineData(BarberState.Called)]
    [InlineData(BarberState.InService)]
    public void StartService_WithDifferentBusyStation_ReservesTicketForScannedBarber(BarberState busyState)
    {
        using var database = TestDatabase.Create();
        var now = OperationalClock.Now;
        var assignedBarberId = Guid.NewGuid();
        var scannedBarberId = Guid.NewGuid();
        var turnId = Guid.NewGuid();

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            var barberRepository = new LocalBarberRepository(connection);
            var turnRepository = new LocalTurnRepository(connection);
            barberRepository.Upsert(new Barber(assignedBarberId, "Ana", BarberState.Called, 0, 0, now.AddMinutes(-10), stationNumber: 1), now);
            barberRepository.Upsert(new Barber(scannedBarberId, "Luis", busyState, 0, 1, now.AddMinutes(-8), stationNumber: 2), now);
            turnRepository.Upsert(CreateTurn(turnId, "W202606261000004", 80, TurnState.Called, now.AddMinutes(-5), assignedBarberId), now);
        }

        var result = new BarberPanelService(database.ConnectionFactory).StartService("B-2", "80");

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var barberRepositoryVerify = new LocalBarberRepository(verifyConnection);
        var savedAssignedBarber = barberRepositoryVerify.GetById(assignedBarberId);
        var savedScannedBarber = barberRepositoryVerify.GetById(scannedBarberId);
        var savedTurn = new LocalTurnRepository(verifyConnection).GetById(turnId);
        var outboxEvents = new SyncOutboxRepository(verifyConnection).ListAll();

        Assert.Equal(BarberPanelStartOutcome.ReassignedToWaiting, result.Outcome);
        Assert.Equal(scannedBarberId, result.BarberId);
        Assert.Equal(TurnState.Waiting, savedTurn?.State);
        Assert.Null(savedTurn?.AssignedBarberId);
        Assert.Equal([scannedBarberId], savedTurn?.RequestedBarberIds);
        Assert.Equal(BarberState.Available, savedAssignedBarber?.State);
        Assert.Equal(busyState, savedScannedBarber?.State);
        Assert.Null(savedTurn?.StartedAt);
        Assert.Contains(outboxEvents, outboxEvent => outboxEvent.EventType == "ticket.called" && outboxEvent.AggregateId == turnId);
        Assert.DoesNotContain(outboxEvents, outboxEvent => outboxEvent.EventType == "ticket.started" && outboxEvent.AggregateId == turnId);
        Assert.Contains(outboxEvents, outboxEvent => outboxEvent.EventType == "ticket.auto_reassigned" && outboxEvent.AggregateId == turnId);
    }

    [Theory]
    [InlineData(BarberState.NotCheckedIn)]
    [InlineData(BarberState.Offline)]
    public void StartService_WithDifferentUnavailableStation_RejectsWithoutMutatingState(BarberState unavailableState)
    {
        using var database = TestDatabase.Create();
        var now = OperationalClock.Now;
        var assignedBarberId = Guid.NewGuid();
        var scannedBarberId = Guid.NewGuid();
        var turnId = Guid.NewGuid();

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            var barberRepository = new LocalBarberRepository(connection);
            var turnRepository = new LocalTurnRepository(connection);
            barberRepository.Upsert(new Barber(assignedBarberId, "Ana", BarberState.Called, 0, 0, now.AddMinutes(-10), stationNumber: 1), now);
            barberRepository.Upsert(new Barber(scannedBarberId, "Luis", unavailableState, 0, 1, now.AddMinutes(-8), stationNumber: 2), now);
            turnRepository.Upsert(CreateTurn(turnId, "W202606261000005", 81, TurnState.Called, now.AddMinutes(-5), assignedBarberId), now);
        }

        var exception = Assert.Throws<InvalidOperationException>(
            () => new BarberPanelService(database.ConnectionFactory).StartService("B-2", "81"));

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var barberRepositoryVerify = new LocalBarberRepository(verifyConnection);
        var savedAssignedBarber = barberRepositoryVerify.GetById(assignedBarberId);
        var savedScannedBarber = barberRepositoryVerify.GetById(scannedBarberId);
        var savedTurn = new LocalTurnRepository(verifyConnection).GetById(turnId);

        Assert.Contains("available, called, or in service", exception.Message);
        Assert.Equal(BarberState.Called, savedAssignedBarber?.State);
        Assert.Equal(unavailableState, savedScannedBarber?.State);
        Assert.Equal(TurnState.Called, savedTurn?.State);
        Assert.Equal(assignedBarberId, savedTurn?.AssignedBarberId);
        Assert.Null(savedTurn?.StartedAt);
        Assert.Empty(new SyncOutboxRepository(verifyConnection).ListAll());
    }

    [Fact]
    public void StartService_WithWaitingReservedTicketAndAvailableStation_ReassignsAndStartsTicket()
    {
        using var database = TestDatabase.Create();
        var now = OperationalClock.Now;
        var requestedBarberId = Guid.NewGuid();
        var scannedBarberId = Guid.NewGuid();
        var turnId = Guid.NewGuid();

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            var barberRepository = new LocalBarberRepository(connection);
            var turnRepository = new LocalTurnRepository(connection);
            barberRepository.Upsert(new Barber(requestedBarberId, "Ana", BarberState.InService, 0, 0, now.AddMinutes(-10), stationNumber: 1), now);
            barberRepository.Upsert(new Barber(scannedBarberId, "Luis", BarberState.Available, 0, 1, now.AddMinutes(-8), stationNumber: 2), now);
            turnRepository.Upsert(CreateTurn(
                turnId,
                "W202606261000006",
                82,
                TurnState.Waiting,
                now.AddMinutes(-5),
                requestedBarberIds: [requestedBarberId]), now);
        }

        var result = new BarberPanelService(database.ConnectionFactory).StartService("B-2", "82");

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var savedScannedBarber = new LocalBarberRepository(verifyConnection).GetById(scannedBarberId);
        var savedTurn = new LocalTurnRepository(verifyConnection).GetById(turnId);
        var outboxEvents = new SyncOutboxRepository(verifyConnection).ListAll();

        Assert.Equal(BarberPanelStartOutcome.Started, result.Outcome);
        Assert.Equal(BarberState.InService, savedScannedBarber?.State);
        Assert.Equal(TurnState.InService, savedTurn?.State);
        Assert.Equal(scannedBarberId, savedTurn?.AssignedBarberId);
        Assert.Equal([scannedBarberId], savedTurn?.RequestedBarberIds);
        Assert.Contains(outboxEvents, outboxEvent => outboxEvent.EventType == "ticket.called" && outboxEvent.AggregateId == turnId);
        Assert.Contains(outboxEvents, outboxEvent => outboxEvent.EventType == "ticket.started" && outboxEvent.AggregateId == turnId);
        Assert.Contains(outboxEvents, outboxEvent => outboxEvent.EventType == "ticket.auto_reassigned" && outboxEvent.AggregateId == turnId);
    }

    [Fact]
    public void StartService_WhenPreviousBarberIsReleased_AssignsNextCompatibleWaitingTicket()
    {
        using var database = TestDatabase.Create();
        var now = OperationalClock.Now;
        var previousBarberId = Guid.NewGuid();
        var scannedBarberId = Guid.NewGuid();
        var transferredTurnId = Guid.NewGuid();
        var nextTurnId = Guid.NewGuid();

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            var barberRepository = new LocalBarberRepository(connection);
            var turnRepository = new LocalTurnRepository(connection);
            var businessDate = OperationalClock.GetBusinessDate(now);
            barberRepository.Upsert(new Barber(previousBarberId, "Ana", BarberState.Called, 0, 0, now.AddMinutes(-10), stationNumber: 1), now);
            barberRepository.Upsert(new Barber(scannedBarberId, "Luis", BarberState.Available, 0, 1, now.AddMinutes(-8), stationNumber: 2), now);
            new DailyRotationRepository(connection).EnsureQueued(businessDate, previousBarberId, now.AddMinutes(-10), now);
            turnRepository.Upsert(CreateTurn(transferredTurnId, "W202606261000007", 83, TurnState.Called, now.AddMinutes(-5), previousBarberId), now);
            turnRepository.Upsert(CreateTurn(nextTurnId, "W202606261000008", 84, TurnState.Waiting, now.AddMinutes(-4)), now);
        }

        new BarberPanelService(database.ConnectionFactory).StartService("B-2", "83");

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var barberRepositoryVerify = new LocalBarberRepository(verifyConnection);
        var turnRepositoryVerify = new LocalTurnRepository(verifyConnection);
        var savedPreviousBarber = barberRepositoryVerify.GetById(previousBarberId);
        var savedScannedBarber = barberRepositoryVerify.GetById(scannedBarberId);
        var savedTransferredTurn = turnRepositoryVerify.GetById(transferredTurnId);
        var savedNextTurn = turnRepositoryVerify.GetById(nextTurnId);
        var outboxEvents = new SyncOutboxRepository(verifyConnection).ListAll();

        Assert.Equal(BarberState.Called, savedPreviousBarber?.State);
        Assert.Equal(BarberState.InService, savedScannedBarber?.State);
        Assert.Equal(TurnState.InService, savedTransferredTurn?.State);
        Assert.Equal(scannedBarberId, savedTransferredTurn?.AssignedBarberId);
        Assert.Equal(TurnState.Called, savedNextTurn?.State);
        Assert.Equal(previousBarberId, savedNextTurn?.AssignedBarberId);
        Assert.Contains(outboxEvents, outboxEvent => outboxEvent.EventType == "ticket.called" && outboxEvent.AggregateId == nextTurnId);
        Assert.Contains(outboxEvents, outboxEvent => outboxEvent.EventType == "ticket.auto_reassigned" && outboxEvent.AggregateId == transferredTurnId);
    }

    [Fact]
    public void StartService_WithUnknownStation_RejectsWithoutMutatingState()
    {
        using var database = TestDatabase.Create();
        var now = OperationalClock.Now;
        var barberId = Guid.NewGuid();
        var turnId = Guid.NewGuid();

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            var barberRepository = new LocalBarberRepository(connection);
            var turnRepository = new LocalTurnRepository(connection);
            barberRepository.Upsert(new Barber(barberId, "Ana", BarberState.Called, 0, 0, now.AddMinutes(-10), stationNumber: 1), now);
            turnRepository.Upsert(CreateTurn(turnId, "W202606261000003", 79, TurnState.Called, now.AddMinutes(-5), barberId), now);
        }

        var exception = Assert.Throws<InvalidOperationException>(
            () => new BarberPanelService(database.ConnectionFactory).StartService("B-9", "79"));

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var savedBarber = new LocalBarberRepository(verifyConnection).GetById(barberId);
        var savedTurn = new LocalTurnRepository(verifyConnection).GetById(turnId);

        Assert.Contains("active barber", exception.Message);
        Assert.Equal(BarberState.Called, savedBarber?.State);
        Assert.Equal(TurnState.Called, savedTurn?.State);
        Assert.Null(savedTurn?.StartedAt);
        Assert.Empty(new SyncOutboxRepository(verifyConnection).ListAll());
    }

    [Fact]
    public void StartService_WithAppointmentQrAndDifferentStation_RejectsWithoutMutatingState()
    {
        using var database = TestDatabase.Create();
        var now = OperationalClock.Now;
        var appointmentBarberId = Guid.NewGuid();
        var scannedBarberId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        const string appointmentCode = "A123456789ABC";

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            var barberRepository = new LocalBarberRepository(connection);
            barberRepository.Upsert(new Barber(appointmentBarberId, "Ana", BarberState.Available, 0, 0, now.AddMinutes(-10), stationNumber: 1), now);
            barberRepository.Upsert(new Barber(scannedBarberId, "Luis", BarberState.Available, 0, 1, now.AddMinutes(-8), stationNumber: 2), now);
            new AppointmentReservationRepository(connection).Upsert(
                new AppointmentReservation(
                    appointmentId,
                    appointmentBarberId,
                    AppointmentState.Confirmed,
                    now.AddMinutes(1),
                    AppointmentReservation.DefaultProtectionWindow,
                    appointmentCode: appointmentCode,
                    customerName: "Cliente prueba"),
                now);
        }

        var exception = Assert.Throws<InvalidOperationException>(
            () => new BarberPanelService(database.ConnectionFactory).StartService("B-2", appointmentCode));

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var appointment = new AppointmentReservationRepository(verifyConnection).GetById(appointmentId);
        var localTurn = new LocalTurnRepository(verifyConnection).GetByAppointmentId(appointmentId);
        var barberRepositoryVerify = new LocalBarberRepository(verifyConnection);

        Assert.Contains("appointment barber", exception.Message);
        Assert.Equal(AppointmentState.Confirmed, appointment?.State);
        Assert.Null(appointment?.CheckedInAt);
        Assert.Null(localTurn);
        Assert.Equal(BarberState.Available, barberRepositoryVerify.GetById(appointmentBarberId)?.State);
        Assert.Equal(BarberState.Available, barberRepositoryVerify.GetById(scannedBarberId)?.State);
        Assert.Empty(new SyncOutboxRepository(verifyConnection).ListAll());
    }

    private static Turn CreateTurn(
        Guid id,
        string ticketNumber,
        int displayTicketNumber,
        TurnState state,
        DateTimeOffset checkedInAt,
        Guid? assignedBarberId = null,
        IReadOnlyCollection<Guid>? requestedBarberIds = null)
    {
        return new Turn(
            id,
            ticketNumber,
            displayTicketNumber,
            OperationalClock.GetBusinessDate(checkedInAt),
            state,
            TurnSource.WalkIn,
            checkedInAt,
            assignedBarberId,
            requestedBarberIds: requestedBarberIds);
    }

    private sealed class TestDatabase : IDisposable
    {
        private readonly IDisposable _keepAliveConnection;

        private TestDatabase(SqliteConnectionFactory connectionFactory, IDisposable keepAliveConnection)
        {
            ConnectionFactory = connectionFactory;
            _keepAliveConnection = keepAliveConnection;
        }

        public SqliteConnectionFactory ConnectionFactory { get; }

        public static TestDatabase Create()
        {
            var name = Guid.NewGuid().ToString("N");
            var connectionFactory = new SqliteConnectionFactory($"Data Source={name};Mode=Memory;Cache=Shared");
            var keepAliveConnection = connectionFactory.OpenConnection();
            LocalDatabaseInitializer.Initialize(keepAliveConnection);

            return new TestDatabase(connectionFactory, keepAliveConnection);
        }

        public void Dispose()
        {
            _keepAliveConnection.Dispose();
        }
    }
}
