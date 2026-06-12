using System;
using System.Collections.Generic;
using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Barberia.Desktop.Services;
using Xunit;

namespace Barberia.Desktop.Tests;

public class LocalAdminServiceTests
{
    [Fact]
    public void SaveBarber_WithCommissionPercentage_PersistsBarberCommission()
    {
        using var database = TestDatabase.Create();
        var service = new LocalAdminService(database.ConnectionFactory);

        service.SaveBarber(
            null,
            "Ana",
            stationNumber: 1,
            profileImagePath: null,
            isActive: true,
            commissionPercentage: 70);

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var barber = Assert.Single(new LocalBarberRepository(verifyConnection).ListAll());
        var auditEvents = new AuditEventRepository(verifyConnection).ListAll();

        Assert.Equal("Ana", barber.DisplayName);
        Assert.Equal(70, barber.CommissionPercentage);
        Assert.Contains(auditEvents, auditEvent => auditEvent.EventType == "admin_barber_created" && auditEvent.Payload.Contains("\"CommissionPercentage\":70"));
    }

    [Fact]
    public void DeactivateBarber_ReleasesStationNumberAndSetsOffline()
    {
        using var database = TestDatabase.Create();
        var service = new LocalAdminService(database.ConnectionFactory);

        service.SaveBarber(null, "Ana", 1, null, true, 50);

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var barberId = new LocalBarberRepository(verifyConnection).ListAll()[0].Id;

        service.DeactivateBarber(barberId);

        var savedBarber = new LocalBarberRepository(verifyConnection).GetById(barberId);
        Assert.False(savedBarber!.IsActive);
        Assert.Equal(BarberState.Offline, savedBarber.State);
        Assert.Null(savedBarber.StationNumber);
    }

    [Fact]
    public void ActivateBarber_RequiresEditorSaveWithStation()
    {
        using var database = TestDatabase.Create();
        var service = new LocalAdminService(database.ConnectionFactory);

        service.SaveBarber(null, "Ana", 1, null, false, 50);

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var barberId = new LocalBarberRepository(verifyConnection).ListAll()[0].Id;

        var exception = Assert.Throws<InvalidOperationException>(() => service.ActivateBarber(barberId));

        Assert.Contains("barber editor", exception.Message);
    }

    [Fact]
    public void SaveBarber_InactiveBarberWithStation_ReleasesStationNumber()
    {
        using var database = TestDatabase.Create();
        var service = new LocalAdminService(database.ConnectionFactory);

        service.SaveBarber(null, "Luis", null, null, false, 65);

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var barberId = new LocalBarberRepository(verifyConnection).ListAll()[0].Id;

        service.SaveBarber(barberId, "Luis", 5, null, false, 65);

        var savedBarber = new LocalBarberRepository(verifyConnection).GetById(barberId);
        Assert.False(savedBarber!.IsActive);
        Assert.Equal(BarberState.Offline, savedBarber.State);
        Assert.Null(savedBarber.StationNumber);
    }

    [Fact]
    public void SaveBarber_ReactivatesInactiveBarberWithStation_AsAvailable()
    {
        using var database = TestDatabase.Create();
        var service = new LocalAdminService(database.ConnectionFactory);

        service.SaveBarber(null, "Luis", null, null, false, 65);

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var barberId = new LocalBarberRepository(verifyConnection).ListAll()[0].Id;

        service.SaveBarber(barberId, "Luis", 5, null, true, 65);

        var savedBarber = new LocalBarberRepository(verifyConnection).GetById(barberId);
        Assert.True(savedBarber!.IsActive);
        Assert.Equal(BarberState.Offline, savedBarber.State);
        Assert.Equal(5, savedBarber.StationNumber);
    }

    [Fact]
    public void SaveBarber_ReactivatingInactiveBarberWithoutStation_ShowsStationRequiredMessage()
    {
        using var database = TestDatabase.Create();
        var service = new LocalAdminService(database.ConnectionFactory);

        service.SaveBarber(null, "Luis", null, null, false, 65);

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var barberId = new LocalBarberRepository(verifyConnection).ListAll()[0].Id;

        var exception = Assert.Throws<InvalidOperationException>(
            () => service.SaveBarber(barberId, "Luis", null, null, true, 65));

        Assert.Contains("Write a station number", exception.Message);
    }

    [Fact]
    public void MarkBarberAvailable_QueuesByFirstArrivalAndPreservesSameDayReentry()
    {
        using var database = TestDatabase.Create();
        var service = new LocalAdminService(database.ConnectionFactory);

        service.SaveBarber(null, "Ana", 1, null, true, 65);
        service.SaveBarber(null, "Luis", 2, null, true, 65);

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var barberRepository = new LocalBarberRepository(verifyConnection);
        var barbers = barberRepository.ListAll();
        var anaId = barbers.Single(barber => barber.DisplayName == "Ana").Id;
        var luisId = barbers.Single(barber => barber.DisplayName == "Luis").Id;

        service.MarkBarberAvailable(anaId);
        service.MarkBarberAvailable(luisId);
        service.MarkBarberOffline(anaId);
        var initialAnaEntry = new DailyRotationRepository(verifyConnection)
            .ListByDate(DateOnly.FromDateTime(DateTimeOffset.Now.LocalDateTime))
            .Single(entry => entry.BarberId == anaId);

        service.MarkBarberAvailable(anaId);

        var entries = new DailyRotationRepository(verifyConnection)
            .ListByDate(DateOnly.FromDateTime(DateTimeOffset.Now.LocalDateTime));
        var savedAna = barberRepository.GetById(anaId);

        Assert.Equal(BarberState.Available, savedAna?.State);
        Assert.NotNull(savedAna?.CheckedInAt);
        Assert.Collection(
            entries,
            entry =>
            {
                Assert.Equal(anaId, entry.BarberId);
                Assert.Equal(initialAnaEntry.ArrivedAt, entry.ArrivedAt);
            },
            entry => Assert.Equal(luisId, entry.BarberId));
    }

    [Fact]
    public void Load_AppliesPendingDailyResetForPreviousDayActiveTickets()
    {
        using var database = TestDatabase.Create();
        var previousDay = DateTimeOffset.Now.AddDays(-1);
        var barberId = Guid.NewGuid();
        var turnId = Guid.NewGuid();

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            var barberRepository = new LocalBarberRepository(connection);
            var turnRepository = new LocalTurnRepository(connection);
            barberRepository.Upsert(new Barber(barberId, "Ana", BarberState.InService, 3, 0, previousDay, stationNumber: 1), previousDay);
            turnRepository.Upsert(CreateTurn(turnId, "T-099", TurnState.InService, previousDay, barberId), previousDay);
        }

        new LocalAdminService(database.ConnectionFactory).Load();

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var savedBarber = new LocalBarberRepository(verifyConnection).GetById(barberId);
        var savedTurn = new LocalTurnRepository(verifyConnection).GetById(turnId);
        var auditEvents = new AuditEventRepository(verifyConnection).ListAll();

        Assert.Equal(BarberState.Offline, savedBarber?.State);
        Assert.Equal(0, savedBarber?.ClientsServedToday);
        Assert.Null(savedBarber?.CheckedInAt);
        Assert.Equal(TurnState.Cancelled, savedTurn?.State);
        Assert.NotNull(savedTurn?.CancelledAt);
        Assert.Contains(auditEvents, auditEvent => auditEvent.EventType == "daily_operational_reset");
    }

    [Fact]
    public void SaveService_WithExistingService_UpdatesService()
    {
        using var database = TestDatabase.Create();
        var serviceId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-06-08T10:00:00Z");

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO services (
                    id, name, price_cents, is_active, display_order, created_at, updated_at
                ) VALUES (
                    $id, $name, $price_cents, $is_active, $display_order, $created_at, $updated_at
                );
                """;
            command.Parameters.AddWithValue("$id", serviceId.ToString("N"));
            command.Parameters.AddWithValue("$name", "REGULAR HAIRCUT");
            command.Parameters.AddWithValue("$price_cents", 2500);
            command.Parameters.AddWithValue("$is_active", 1);
            command.Parameters.AddWithValue("$display_order", 0);
            command.Parameters.AddWithValue("$created_at", now.ToString("O"));
            command.Parameters.AddWithValue("$updated_at", now.ToString("O"));
            command.ExecuteNonQuery();
        }

        new LocalAdminService(database.ConnectionFactory).SaveService(
            serviceId,
            "REGULAR HAIRCUT",
            30m,
            isActive: false,
            3);

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var services = new ServiceRepository(verifyConnection).ListAll();
        var saved = Assert.Single(services);
        var auditEvents = new AuditEventRepository(verifyConnection).ListAll();

        Assert.Equal(serviceId, saved.Id);
        Assert.Equal("REGULAR HAIRCUT", saved.Name);
        Assert.Equal(30m, saved.Price);
        Assert.False(saved.IsActive);
        Assert.Equal(3, saved.DisplayOrder);
        Assert.Contains(auditEvents, auditEvent => auditEvent.EventType == "admin_service_updated" && auditEvent.AggregateId == serviceId);
    }

    [Fact]
    public void ReassignTurn_CalledTicketToAvailableBarber_CallsTargetAndReleasesPrevious()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Now;
        var juanId = Guid.NewGuid();
        var pedroId = Guid.NewGuid();
        var turnId = Guid.NewGuid();

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            var barberRepository = new LocalBarberRepository(connection);
            var turnRepository = new LocalTurnRepository(connection);
            barberRepository.Upsert(new Barber(juanId, "Juan", BarberState.Called, 1, 0, now, stationNumber: 1), now);
            barberRepository.Upsert(new Barber(pedroId, "Pedro", BarberState.Available, 1, 1, now, stationNumber: 2), now);
            turnRepository.Upsert(CreateTurn(turnId, "T-008", TurnState.Called, now, juanId), now);
        }

        new LocalAdminService(database.ConnectionFactory).ReassignTurn(turnId, pedroId);

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var savedTurn = new LocalTurnRepository(verifyConnection).GetById(turnId);
        var savedJuan = new LocalBarberRepository(verifyConnection).GetById(juanId);
        var savedPedro = new LocalBarberRepository(verifyConnection).GetById(pedroId);
        var auditEvents = new AuditEventRepository(verifyConnection).ListAll();

        Assert.Equal(TurnState.Called, savedTurn?.State);
        Assert.Equal(pedroId, savedTurn?.AssignedBarberId);
        Assert.Equal([pedroId], savedTurn?.RequestedBarberIds);
        Assert.Equal(BarberState.Available, savedJuan?.State);
        Assert.Equal(BarberState.Called, savedPedro?.State);
        Assert.Contains(auditEvents, auditEvent => auditEvent.EventType == "admin_turn_reassigned" && auditEvent.AggregateId == turnId);
    }

    [Fact]
    public void ReassignTurn_CalledTicketToBusyBarber_ReservesTicketForTarget()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Now;
        var juanId = Guid.NewGuid();
        var pedroId = Guid.NewGuid();
        var turnId = Guid.NewGuid();

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            var barberRepository = new LocalBarberRepository(connection);
            var turnRepository = new LocalTurnRepository(connection);
            barberRepository.Upsert(new Barber(juanId, "Juan", BarberState.Called, 1, 0, now, stationNumber: 1), now);
            barberRepository.Upsert(new Barber(pedroId, "Pedro", BarberState.InService, 1, 1, now, stationNumber: 2), now);
            turnRepository.Upsert(CreateTurn(turnId, "T-008", TurnState.Called, now, juanId), now);
        }

        new LocalAdminService(database.ConnectionFactory).ReassignTurn(turnId, pedroId);

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var savedTurn = new LocalTurnRepository(verifyConnection).GetById(turnId);
        var savedJuan = new LocalBarberRepository(verifyConnection).GetById(juanId);
        var savedPedro = new LocalBarberRepository(verifyConnection).GetById(pedroId);

        Assert.Equal(TurnState.Waiting, savedTurn?.State);
        Assert.Null(savedTurn?.AssignedBarberId);
        Assert.Equal([pedroId], savedTurn?.RequestedBarberIds);
        Assert.Equal(BarberState.Available, savedJuan?.State);
        Assert.Equal(BarberState.InService, savedPedro?.State);
    }

    [Fact]
    public void ReassignTurn_WhenPreviousBarberIsReleased_AssignsNextCompatibleWaitingTicket()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Now;
        var juanId = Guid.NewGuid();
        var pedroId = Guid.NewGuid();
        var reassignedTurnId = Guid.NewGuid();
        var nextTurnId = Guid.NewGuid();

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            var barberRepository = new LocalBarberRepository(connection);
            var turnRepository = new LocalTurnRepository(connection);
            barberRepository.Upsert(new Barber(juanId, "Juan", BarberState.Called, 1, 0, now, stationNumber: 1), now);
            barberRepository.Upsert(new Barber(pedroId, "Pedro", BarberState.InService, 1, 1, now, stationNumber: 2), now);
            turnRepository.Upsert(CreateTurn(reassignedTurnId, "T-008", TurnState.Called, now, juanId), now);
            turnRepository.Upsert(CreateTurn(nextTurnId, "T-009", TurnState.Waiting, now.AddMinutes(5)), now);
        }

        new LocalAdminService(database.ConnectionFactory).ReassignTurn(reassignedTurnId, pedroId);

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var turnRepositoryVerify = new LocalTurnRepository(verifyConnection);
        var savedReassignedTurn = turnRepositoryVerify.GetById(reassignedTurnId);
        var savedNextTurn = turnRepositoryVerify.GetById(nextTurnId);
        var savedJuan = new LocalBarberRepository(verifyConnection).GetById(juanId);
        var auditEvents = new AuditEventRepository(verifyConnection).ListAll();

        Assert.Equal(TurnState.Waiting, savedReassignedTurn?.State);
        Assert.Null(savedReassignedTurn?.AssignedBarberId);
        Assert.Equal([pedroId], savedReassignedTurn?.RequestedBarberIds);
        Assert.Equal(TurnState.Called, savedNextTurn?.State);
        Assert.Equal(juanId, savedNextTurn?.AssignedBarberId);
        Assert.Equal(BarberState.Called, savedJuan?.State);
        Assert.Contains(auditEvents, auditEvent => auditEvent.EventType == "admin_waiting_turn_assigned" && auditEvent.AggregateId == nextTurnId);
    }

    [Fact]
    public void ReassignTurn_RejectsInServiceTicket()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Now;
        var barberId = Guid.NewGuid();
        var targetBarberId = Guid.NewGuid();
        var turnId = Guid.NewGuid();

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            var barberRepository = new LocalBarberRepository(connection);
            var turnRepository = new LocalTurnRepository(connection);
            barberRepository.Upsert(new Barber(barberId, "Juan", BarberState.InService, 1, 0, now, stationNumber: 1), now);
            barberRepository.Upsert(new Barber(targetBarberId, "Pedro", BarberState.Available, 1, 1, now, stationNumber: 2), now);
            turnRepository.Upsert(CreateTurn(turnId, "T-010", TurnState.InService, now, barberId), now);
        }

        var exception = Assert.Throws<InvalidOperationException>(
            () => new LocalAdminService(database.ConnectionFactory).ReassignTurn(turnId, targetBarberId));

        Assert.Contains("waiting or called", exception.Message);
    }

    [Fact]
    public void ReassignTurn_RejectsInactiveTargetBarber()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Now;
        var targetBarberId = Guid.NewGuid();
        var turnId = Guid.NewGuid();

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            var barberRepository = new LocalBarberRepository(connection);
            var turnRepository = new LocalTurnRepository(connection);
            barberRepository.Upsert(new Barber(targetBarberId, "Pedro", BarberState.Offline, 0, 1, now, isActive: false), now);
            turnRepository.Upsert(CreateTurn(turnId, "T-011", TurnState.Waiting, now), now);
        }

        var exception = Assert.Throws<InvalidOperationException>(
            () => new LocalAdminService(database.ConnectionFactory).ReassignTurn(turnId, targetBarberId));

        Assert.Contains("inactive", exception.Message);
    }

    [Fact]
    public void CalculateAlerts_WithEmptyQueue_ReturnsNoAlerts()
    {
        var alerts = LocalAdminService.CalculateAlerts([], DateTimeOffset.Now, []);
        Assert.Empty(alerts);
    }

    [Fact]
    public void CalculateAlerts_WithWaitingMoreThan30Minutes_ReturnsWarningAlert()
    {
        var now = DateTimeOffset.Now;
        var turns = new[]
        {
            new ActiveTurnRow(
                new Turn(Guid.NewGuid(), "W1", 1, DateOnly.FromDateTime(now.DateTime), TurnState.Waiting, TurnSource.WalkIn, now.AddMinutes(-31), null, null, null, null, null, null, null),
                now.AddMinutes(-31))
        };

        var alerts = LocalAdminService.CalculateAlerts(turns, now, []);

        var alert = Assert.Single(alerts);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
        Assert.Equal(31, alert.ElapsedMinutes);
        Assert.Contains("waiting more than 30 minutes", alert.Title);
    }

    [Fact]
    public void CalculateAlerts_WithWaitingExactly30Minutes_ReturnsNoAlerts()
    {
        var now = DateTimeOffset.Now;
        var turns = new[]
        {
            new ActiveTurnRow(
                new Turn(Guid.NewGuid(), "W1", 1, DateOnly.FromDateTime(now.DateTime), TurnState.Waiting, TurnSource.WalkIn, now.AddMinutes(-30), null, null, null, null, null, null, null),
                now.AddMinutes(-30))
        };

        var alerts = LocalAdminService.CalculateAlerts(turns, now, []);

        Assert.Empty(alerts);
    }

    [Fact]
    public void CalculateAlerts_WithCalledMoreThan4Minutes_ReturnsCriticalAlert()
    {
        var now = DateTimeOffset.Now;
        var barberId = Guid.NewGuid();
        var turns = new[]
        {
            new ActiveTurnRow(
                new Turn(Guid.NewGuid(), "W1", 1, DateOnly.FromDateTime(now.DateTime), TurnState.Called, TurnSource.WalkIn, now.AddMinutes(-10), barberId, null, null, null, null, null, null),
                now.AddMinutes(-5))
        };
        var barbers = new[]
        {
            new Barber(barberId, "Marcus", BarberState.Called, 0, 1, null, 1, null, true)
        };

        var alerts = LocalAdminService.CalculateAlerts(turns, now, barbers);

        var alert = Assert.Single(alerts);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Equal(5, alert.ElapsedMinutes);
        Assert.Contains("not started", alert.Title);
        Assert.Contains("Marcus", alert.Detail);
    }

    [Fact]
    public void CalculateAlerts_WithInServiceOrCompleted_ReturnsNoAlerts()
    {
        var now = DateTimeOffset.Now;
        var barberId = Guid.NewGuid();
        var turns = new[]
        {
            new ActiveTurnRow(
                new Turn(Guid.NewGuid(), "W1", 1, DateOnly.FromDateTime(now.DateTime), TurnState.InService, TurnSource.WalkIn, now.AddMinutes(-10), barberId, null, null, null, now.AddMinutes(-5), null, null),
                now.AddMinutes(-5)),
            new ActiveTurnRow(
                new Turn(Guid.NewGuid(), "W2", 2, DateOnly.FromDateTime(now.DateTime), TurnState.Completed, TurnSource.WalkIn, now.AddMinutes(-10), barberId, null, null, null, now.AddMinutes(-5), now, null),
                now)
        };

        var alerts = LocalAdminService.CalculateAlerts(turns, now, []);

        Assert.Empty(alerts);
    }

    private static Turn CreateTurn(
        Guid id,
        string ticketNumber,
        TurnState state,
        DateTimeOffset checkedInAt,
        Guid? assignedBarberId = null,
        IReadOnlyCollection<Guid>? requestedBarberIds = null)
    {
        return new Turn(
            id,
            ticketNumber,
            ParseDisplayTicketNumber(ticketNumber),
            DateOnly.FromDateTime(checkedInAt.LocalDateTime),
            state,
            TurnSource.WalkIn,
            checkedInAt,
            assignedBarberId,
            requestedBarberIds: requestedBarberIds);
    }

    private static int ParseDisplayTicketNumber(string ticketNumber)
    {
        var suffix = ticketNumber.Split('-').LastOrDefault();
        return int.TryParse(suffix, out var number) && number > 0 ? number : 1;
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
