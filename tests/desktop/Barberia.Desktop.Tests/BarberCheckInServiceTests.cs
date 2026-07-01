using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Repositories;
using Barberia.Data.Sync;
using Barberia.Desktop.Services;
using Barberia.Hardware.Pos;
using Barberia.Sync.Outbox;
using Xunit;

namespace Barberia.Desktop.Tests;

public sealed class BarberCheckInServiceTests
{
    [Fact]
    public void KioskRegisterWalkIn_WithSelectableButNotCheckedInBarber_PrintsWaitingTicket()
    {
        using var database = TestDatabase.Create();
        var barberId = Guid.NewGuid();
        SeedSelectableBarber(database, barberId, "Ana", 1);

        var result = new KioskCheckInService(database.ConnectionFactory, new SimulatedKioskTicketPrinter())
            .RegisterWalkIn("Cliente", acceptsAnyBarber: true, Array.Empty<Guid>());

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var savedTurn = new LocalTurnRepository(verifyConnection).GetByTicketNumber(result.InternalTicketNumber);
        var savedBarber = new LocalBarberRepository(verifyConnection).GetById(barberId);
        var rotationEntries = new DailyRotationRepository(verifyConnection)
            .ListByDate(OperationalClock.GetBusinessDate(result.CheckedInAt));

        Assert.Equal(KioskCheckInStatus.Waiting, result.Status);
        Assert.Equal(result.InternalTicketNumber, result.QrPayload);
        Assert.Equal(TurnState.Waiting, savedTurn?.State);
        Assert.Null(savedTurn?.AssignedBarberId);
        Assert.Equal(BarberState.Available, savedBarber?.State);
        Assert.Null(savedBarber?.CheckedInAt);
        Assert.Empty(rotationEntries);
    }

    [Fact]
    public void RemoteKioskPrintTicketLocally_PrintsTicketWithPc1DeviceId()
    {
        var printedJobs = new List<KioskTicketPrintJob>();
        var printer = new CapturingKioskTicketPrinter(printedJobs, HardwareOperationResult.Success());
        var checkedInAt = DateTimeOffset.Parse("2026-06-30T21:00:00-04:00");
        var result = new KioskCheckInResult(
            12,
            "W20260630210000001",
            "W20260630210000001",
            "Cliente",
            checkedInAt,
            "Ana",
            "B-1",
            ["Ana"],
            ["B-1"],
            AcceptsAnyBarber: false,
            KioskCheckInStatus.Assigned,
            "Ticket printed.");

        RemoteKioskStationService.PrintTicketLocally(result, printer, "PC1-KIOSK");

        var job = Assert.Single(printedJobs);
        Assert.Equal(12, job.DisplayTicketNumber);
        Assert.Equal(result.QrPayload, job.QrPayload);
        Assert.Equal("PC1-KIOSK", job.DeviceId);
        Assert.Equal(result.RequestedBarberNames, job.RequestedBarberNames);
        Assert.Equal(result.RequestedBarberStationCodes, job.RequestedBarberStationCodes);
    }

    [Fact]
    public void RemoteKioskPrintTicketLocally_WhenPrinterFails_ReportsRegisteredTicket()
    {
        var printer = new CapturingKioskTicketPrinter([], HardwareOperationResult.Failure("No default printer."));
        var result = new KioskCheckInResult(
            12,
            "W20260630210000001",
            "W20260630210000001",
            "Cliente",
            DateTimeOffset.Parse("2026-06-30T21:00:00-04:00"),
            null,
            null,
            [],
            [],
            AcceptsAnyBarber: true,
            KioskCheckInStatus.Waiting,
            "Ticket printed.");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RemoteKioskStationService.PrintTicketLocally(result, printer, "PC1-KIOSK"));

        Assert.Contains("registered on PC3", exception.Message);
        Assert.Contains("No default printer", exception.Message);

    }

    [Fact]
    public void CheckIn_WithStation_CreatesRotationAndAssignsWaitingTicket()
    {
        using var database = TestDatabase.Create();
        var barberId = Guid.NewGuid();
        SeedSelectableBarber(database, barberId, "Ana", 1);
        var ticket = new KioskCheckInService(database.ConnectionFactory, new SimulatedKioskTicketPrinter())
            .RegisterWalkIn("Cliente", acceptsAnyBarber: true, Array.Empty<Guid>());

        var result = new BarberCheckInService(database.ConnectionFactory).CheckIn("b-1");

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var savedTurn = new LocalTurnRepository(verifyConnection).GetByTicketNumber(ticket.InternalTicketNumber);
        var savedBarber = new LocalBarberRepository(verifyConnection).GetById(barberId);
        var rotationEntry = Assert.Single(new DailyRotationRepository(verifyConnection)
            .ListByDate(OperationalClock.GetBusinessDate(result.ArrivedAt)));
        var outboxEvents = new SyncOutboxRepository(verifyConnection).ListAll();

        Assert.Equal(barberId, result.BarberId);
        Assert.Equal(1, result.QueuePosition);
        Assert.Equal(ticket.DisplayTicketNumber, result.AssignedDisplayTicketNumber);
        Assert.Equal(TurnState.Called, savedTurn?.State);
        Assert.Equal(barberId, savedTurn?.AssignedBarberId);
        Assert.Equal(BarberState.Called, savedBarber?.State);
        Assert.NotNull(savedBarber?.CheckedInAt);
        Assert.Equal(barberId, rotationEntry.BarberId);
        Assert.Equal(0, rotationEntry.QueuePosition);
        Assert.Contains(outboxEvents, outboxEvent => outboxEvent.EventType == "ticket.called" && outboxEvent.AggregateId == savedTurn?.Id);
    }

    [Fact]
    public void CheckIn_OrderFollowsStationCheckInArrivalNotStationNumber()
    {
        using var database = TestDatabase.Create();
        var b1Id = Guid.NewGuid();
        var b6Id = Guid.NewGuid();
        SeedSelectableBarber(database, b1Id, "Ana", 1);
        SeedSelectableBarber(database, b6Id, "Luis", 6);
        var kiosk = new KioskCheckInService(database.ConnectionFactory, new SimulatedKioskTicketPrinter());
        var firstTicket = kiosk.RegisterWalkIn("Cliente 1", acceptsAnyBarber: true, Array.Empty<Guid>());
        var secondTicket = kiosk.RegisterWalkIn("Cliente 2", acceptsAnyBarber: true, Array.Empty<Guid>());

        var firstCheckIn = new BarberCheckInService(database.ConnectionFactory).CheckIn("B-6");
        var secondCheckIn = new BarberCheckInService(database.ConnectionFactory).CheckIn("1");

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var turnRepository = new LocalTurnRepository(verifyConnection);
        var savedFirstTurn = turnRepository.GetByTicketNumber(firstTicket.InternalTicketNumber);
        var savedSecondTurn = turnRepository.GetByTicketNumber(secondTicket.InternalTicketNumber);
        var rotationEntries = new DailyRotationRepository(verifyConnection)
            .ListByDate(OperationalClock.GetBusinessDate(firstCheckIn.ArrivedAt));

        Assert.Equal(b6Id, firstCheckIn.BarberId);
        Assert.Equal(b1Id, secondCheckIn.BarberId);
        Assert.Equal(TurnState.Called, savedFirstTurn?.State);
        Assert.Equal(b6Id, savedFirstTurn?.AssignedBarberId);
        Assert.Equal(TurnState.Called, savedSecondTurn?.State);
        Assert.Equal(b1Id, savedSecondTurn?.AssignedBarberId);
        Assert.Collection(
            rotationEntries,
            entry =>
            {
                Assert.Equal(b6Id, entry.BarberId);
                Assert.Equal(0, entry.QueuePosition);
            },
            entry =>
            {
                Assert.Equal(b1Id, entry.BarberId);
                Assert.Equal(1, entry.QueuePosition);
            });
    }

    [Fact]
    public void CheckIn_WithUnknownStation_RejectsWithoutMutatingBarberOrRotation()
    {
        using var database = TestDatabase.Create();
        var barberId = Guid.NewGuid();
        SeedSelectableBarber(database, barberId, "Ana", 1);

        var exception = Assert.Throws<InvalidOperationException>(
            () => new BarberCheckInService(database.ConnectionFactory).CheckIn("B-9"));

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var savedBarber = new LocalBarberRepository(verifyConnection).GetById(barberId);
        var entries = new DailyRotationRepository(verifyConnection)
            .ListByDate(OperationalClock.GetBusinessDate(OperationalClock.Now));

        Assert.Contains("B-9", exception.Message);
        Assert.Equal(BarberState.Available, savedBarber?.State);
        Assert.Null(savedBarber?.CheckedInAt);
        Assert.Empty(entries);
    }

    private static void SeedSelectableBarber(TestDatabase database, Guid barberId, string displayName, int stationNumber)
    {
        var now = OperationalClock.Now;
        using var connection = database.ConnectionFactory.OpenConnection();
        new LocalBarberRepository(connection).Upsert(
            new Barber(
                barberId,
                displayName,
                BarberState.Available,
                clientsServedToday: 0,
                rotationOrder: stationNumber - 1,
                checkedInAt: null,
                stationNumber: stationNumber,
                updatedAt: now),
            now);
    }

    private sealed class CapturingKioskTicketPrinter : IKioskTicketPrinter
    {
        private readonly List<KioskTicketPrintJob> _printedJobs;
        private readonly HardwareOperationResult _result;

        public CapturingKioskTicketPrinter(List<KioskTicketPrintJob> printedJobs, HardwareOperationResult result)
        {
            _printedJobs = printedJobs;
            _result = result;
        }

        public HardwareOperationResult Print(KioskTicketPrintJob job)
        {
            _printedJobs.Add(job);
            return _result;
        }
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