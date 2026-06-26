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
    public void StartService_WithDifferentStation_RejectsWithoutMutatingState()
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

        var exception = Assert.Throws<InvalidOperationException>(
            () => new BarberPanelService(database.ConnectionFactory).StartService("B-2", "78"));

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var barberRepositoryVerify = new LocalBarberRepository(verifyConnection);
        var savedAssignedBarber = barberRepositoryVerify.GetById(assignedBarberId);
        var savedScannedBarber = barberRepositoryVerify.GetById(scannedBarberId);
        var savedTurn = new LocalTurnRepository(verifyConnection).GetById(turnId);

        Assert.Contains("Station does not match", exception.Message);
        Assert.Equal(BarberState.Called, savedAssignedBarber?.State);
        Assert.Equal(BarberState.Available, savedScannedBarber?.State);
        Assert.Equal(TurnState.Called, savedTurn?.State);
        Assert.Null(savedTurn?.StartedAt);
        Assert.Empty(new SyncOutboxRepository(verifyConnection).ListAll());
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
        Guid? assignedBarberId = null)
    {
        return new Turn(
            id,
            ticketNumber,
            displayTicketNumber,
            OperationalClock.GetBusinessDate(checkedInAt),
            state,
            TurnSource.WalkIn,
            checkedInAt,
            assignedBarberId);
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