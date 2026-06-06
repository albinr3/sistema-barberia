using Barberia.Core.Domain;
using Barberia.Data.Models;
using Barberia.Data.Reports;
using Barberia.Data.Repositories;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Barberia.Data.Tests;

public sealed class SqlitePersistenceTests
{
    [Fact]
    public void Repositories_PersistBarbersAndWaitingTurns()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Parse("2026-06-03T12:00:00Z");
        var barberId = Guid.NewGuid();
        var requestedBarberId = Guid.NewGuid();

        var barberRepository = new LocalBarberRepository(database.Connection);
        barberRepository.Upsert(new Barber(barberId, " Luis ", BarberState.Available, 0, 1, now, profileImagePath: " Assets/barber1.png ", isActive: false), now);
        barberRepository.Upsert(new Barber(requestedBarberId, "Ana", BarberState.Available, 1, 2, now, stationNumber: 1), now);

        var turnRepository = new LocalTurnRepository(database.Connection);
        var laterTurn = CreateTurn(
            Guid.NewGuid(),
            "A-002",
            TurnState.Waiting,
            TurnSource.WalkIn,
            now.AddMinutes(2));
        var firstTurn = CreateTurn(
            Guid.NewGuid(),
            "A-001",
            TurnState.Waiting,
            TurnSource.WalkIn,
            now,
            requestedBarberIds: [requestedBarberId],
            customerName: " Mia ");

        turnRepository.Upsert(laterTurn, now);
        turnRepository.Upsert(firstTurn, now);

        var savedBarber = barberRepository.GetById(barberId);
        var waitingTurns = turnRepository.ListWaiting();

        Assert.NotNull(savedBarber);
        Assert.Equal("Luis", savedBarber.DisplayName);
        Assert.Equal(BarberState.Available, savedBarber.State);
        Assert.Equal("Assets/barber1.png", savedBarber.ProfileImagePath);
        Assert.False(savedBarber.IsActive);
        Assert.Equal(1, barberRepository.GetById(requestedBarberId)?.StationNumber);
        Assert.Collection(
            waitingTurns,
            turn =>
            {
                Assert.Equal(firstTurn.Id, turn.Id);
                Assert.Equal([requestedBarberId], turn.RequestedBarberIds);
                Assert.Equal("Mia", turn.CustomerName);
                Assert.Equal(1, turn.DisplayTicketNumber);
                Assert.Equal(DateOnly.Parse("2026-06-03"), turn.TicketDate);
            },
            turn => Assert.Equal(laterTurn.Id, turn.Id));
    }

    [Fact]
    public void BarberRepository_DeletesBarbersWithoutLocalHistory()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Parse("2026-06-04T10:00:00Z");
        var barberId = Guid.NewGuid();
        var repository = new LocalBarberRepository(database.Connection);

        repository.Upsert(new Barber(barberId, "Mia", BarberState.Offline, 0, 0, now, isActive: false), now);
        repository.Delete(barberId);

        Assert.Null(repository.GetById(barberId));
    }

    [Fact]
    public void BarberRepository_UpdatesActiveFlag()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Parse("2026-06-04T10:30:00Z");
        var barberId = Guid.NewGuid();
        var repository = new LocalBarberRepository(database.Connection);

        repository.Upsert(new Barber(barberId, "Ana", BarberState.Available, 0, 0, now, stationNumber: 1), now);
        repository.SetActive(barberId, isActive: false, now.AddMinutes(1));

        var savedBarber = repository.GetById(barberId);

        Assert.False(savedBarber?.IsActive);
        Assert.Null(savedBarber?.StationNumber);
    }

    [Fact]
    public void BarberRepository_PersistsStationNumber()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Parse("2026-06-04T10:45:00Z");
        var barberId = Guid.NewGuid();
        var repository = new LocalBarberRepository(database.Connection);

        repository.Upsert(new Barber(barberId, "Ana", BarberState.Available, 0, 0, now, stationNumber: 7), now);

        var savedBarber = repository.GetById(barberId);

        Assert.Equal(7, savedBarber?.StationNumber);
        Assert.Equal("B-7", savedBarber?.StationCode);
    }

    [Fact]
    public void BarberRepository_RejectsDuplicateActiveStations()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Parse("2026-06-04T10:50:00Z");
        var repository = new LocalBarberRepository(database.Connection);

        repository.Upsert(new Barber(Guid.NewGuid(), "Ana", BarberState.Available, 0, 0, now, stationNumber: 1), now);

        Assert.Throws<SqliteException>(() =>
            repository.Upsert(new Barber(Guid.NewGuid(), "Luis", BarberState.Available, 0, 1, now, stationNumber: 1), now));
    }

    [Fact]
    public void BarberRepository_ReleasesStationWhenDeactivated()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Parse("2026-06-04T10:55:00Z");
        var barberId = Guid.NewGuid();
        var replacementId = Guid.NewGuid();
        var repository = new LocalBarberRepository(database.Connection);

        repository.Upsert(new Barber(barberId, "Ana", BarberState.Available, 0, 0, now, stationNumber: 1), now);
        repository.SetActive(barberId, isActive: false, now.AddMinutes(1));
        repository.Upsert(new Barber(replacementId, "Luis", BarberState.Available, 0, 1, now, stationNumber: 1), now.AddMinutes(2));

        Assert.Null(repository.GetById(barberId)?.StationNumber);
        Assert.Equal(1, repository.GetById(replacementId)?.StationNumber);
    }

    [Fact]
    public void LocalDatabaseInitializer_AssignsStationsToExistingActiveBarbers()
    {
        using var database = TestDatabase.CreateUninitialized();
        using (var command = database.Connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE barbers (
                    id TEXT NOT NULL PRIMARY KEY,
                    display_name TEXT NOT NULL,
                    state INTEGER NOT NULL,
                    clients_served_today INTEGER NOT NULL,
                    rotation_order INTEGER NOT NULL,
                    checked_in_at TEXT NULL,
                    profile_image_path TEXT NULL,
                    is_active INTEGER NOT NULL DEFAULT 1,
                    updated_at TEXT NOT NULL
                );

                INSERT INTO barbers (id, display_name, state, clients_served_today, rotation_order, checked_in_at, profile_image_path, is_active, updated_at)
                VALUES
                    ('11111111-1111-1111-1111-111111111111', 'Ana', 1, 0, 0, NULL, NULL, 1, '2026-06-04T11:00:00.0000000+00:00'),
                    ('22222222-2222-2222-2222-222222222222', 'Luis', 1, 0, 1, NULL, NULL, 1, '2026-06-04T11:00:00.0000000+00:00'),
                    ('33333333-3333-3333-3333-333333333333', 'Mia', 4, 0, 2, NULL, NULL, 0, '2026-06-04T11:00:00.0000000+00:00');
                """;
            command.ExecuteNonQuery();
        }

        LocalDatabaseInitializer.Initialize(database.Connection);

        var barbers = new LocalBarberRepository(database.Connection).ListAll();

        Assert.Collection(
            barbers,
            barber =>
            {
                Assert.Equal("Ana", barber.DisplayName);
                Assert.Equal(1, barber.StationNumber);
            },
            barber =>
            {
                Assert.Equal("Luis", barber.DisplayName);
                Assert.Equal(2, barber.StationNumber);
            },
            barber =>
            {
                Assert.Equal("Mia", barber.DisplayName);
                Assert.False(barber.IsActive);
                Assert.Null(barber.StationNumber);
            });
    }

    [Fact]
    public void BarberRepository_ListAllUsesRotationOrderNotStationNumber()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Parse("2026-06-04T11:10:00Z");
        var firstBarber = Guid.NewGuid();
        var secondBarber = Guid.NewGuid();
        var repository = new LocalBarberRepository(database.Connection);

        repository.Upsert(new Barber(firstBarber, "Ana", BarberState.Available, 0, 0, now, stationNumber: 9), now);
        repository.Upsert(new Barber(secondBarber, "Luis", BarberState.Available, 0, 1, now, stationNumber: 1), now);

        var barbers = repository.ListAll();

        Assert.Collection(
            barbers,
            barber => Assert.Equal(firstBarber, barber.Id),
            barber => Assert.Equal(secondBarber, barber.Id));
    }

    [Fact]
    public void AppointmentRepository_ReturnsReservationsForLocalCoreQueries()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Parse("2026-06-03T12:00:00Z");
        var barberId = Guid.NewGuid();
        var appointment = new AppointmentReservation(
            Guid.NewGuid(),
            barberId,
            AppointmentState.Confirmed,
            now.AddMinutes(15),
            AppointmentReservation.DefaultProtectionWindow);

        new LocalBarberRepository(database.Connection).Upsert(
            new Barber(barberId, "Ana", BarberState.Available, 0, 0, now, stationNumber: 1),
            now);
        new AppointmentReservationRepository(database.Connection).Upsert(appointment, now);

        var appointments = new AppointmentReservationRepository(database.Connection)
            .ListBetween(now, now.AddHours(1));

        Assert.Collection(
            appointments,
            saved =>
            {
                Assert.Equal(appointment.Id, saved.Id);
                Assert.Equal(AppointmentState.Confirmed, saved.State);
                Assert.Equal(TimeSpan.FromMinutes(15), saved.ProtectionWindow);
            });
    }

    [Fact]
    public void TurnRepository_ReturnsOnlyActiveTurnsForPublicDisplay()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Parse("2026-06-03T12:00:00Z");
        var repository = new LocalTurnRepository(database.Connection);
        var called = CreateTurn(Guid.NewGuid(), "A-002", TurnState.Called, TurnSource.WalkIn, now.AddMinutes(2));
        var assigned = CreateTurn(Guid.NewGuid(), "A-001", TurnState.Assigned, TurnSource.Appointment, now);
        var waiting = CreateTurn(Guid.NewGuid(), "A-003", TurnState.Waiting, TurnSource.WalkIn, now.AddMinutes(3));
        var completed = CreateTurn(Guid.NewGuid(), "A-004", TurnState.Completed, TurnSource.WalkIn, now.AddMinutes(4));

        repository.Upsert(waiting, now);
        repository.Upsert(completed, now);
        repository.Upsert(called, now);
        repository.Upsert(assigned, now);

        var activeTurns = repository.ListActiveForPublicDisplay();

        Assert.Collection(
            activeTurns,
            turn => Assert.Equal(called.Id, turn.Id),
            turn => Assert.Equal(assigned.Id, turn.Id),
            turn => Assert.Equal(waiting.Id, turn.Id));
    }

    [Fact]
    public void TurnRepository_ReturnsAssignedTurnsForSelectedBarber()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Parse("2026-06-03T14:00:00Z");
        var barberId = Guid.NewGuid();
        var otherBarberId = Guid.NewGuid();
        var barberRepository = new LocalBarberRepository(database.Connection);
        var repository = new LocalTurnRepository(database.Connection);

        barberRepository.Upsert(new Barber(barberId, "Luis", BarberState.Called, 0, 0, now, stationNumber: 1), now);
        barberRepository.Upsert(new Barber(otherBarberId, "Ana", BarberState.Called, 0, 1, now, stationNumber: 2), now);

        repository.Upsert(CreateTurn(Guid.NewGuid(), "B-001", TurnState.Assigned, TurnSource.WalkIn, now, barberId), now);
        repository.Upsert(CreateTurn(Guid.NewGuid(), "B-002", TurnState.Called, TurnSource.WalkIn, now.AddMinutes(1), barberId), now);
        repository.Upsert(CreateTurn(Guid.NewGuid(), "B-003", TurnState.InService, TurnSource.WalkIn, now.AddMinutes(2), barberId), now);
        repository.Upsert(CreateTurn(Guid.NewGuid(), "B-004", TurnState.Assigned, TurnSource.WalkIn, now.AddMinutes(3), otherBarberId), now);

        var turns = repository.ListAssignedToBarber(barberId);

        Assert.Collection(
            turns,
            turn => Assert.Equal("B-001", turn.TicketNumber),
            turn => Assert.Equal("B-002", turn.TicketNumber));
    }

    [Fact]
    public void TurnRepository_CancelsActiveTurns()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Parse("2026-06-04T13:00:00Z");
        var turnId = Guid.NewGuid();
        var repository = new LocalTurnRepository(database.Connection);

        repository.Upsert(CreateTurn(turnId, "C-001", TurnState.Called, TurnSource.WalkIn, now), now);
        repository.MarkCancelled(turnId, now.AddMinutes(1));

        Assert.Equal(TurnState.Cancelled, repository.GetById(turnId)?.State);
        Assert.Empty(repository.ListActiveForPublicDisplay());
    }

    [Fact]
    public void Repositories_StartServiceForAssignedTicket()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Parse("2026-06-03T14:30:00Z");
        var barberId = Guid.NewGuid();
        var turnId = Guid.NewGuid();

        var barberRepository = new LocalBarberRepository(database.Connection);
        var turnRepository = new LocalTurnRepository(database.Connection);
        barberRepository.Upsert(new Barber(barberId, "Ana", BarberState.Called, 0, 0, now, stationNumber: 1), now);
        turnRepository.Upsert(CreateTurn(turnId, "B-010", TurnState.Assigned, TurnSource.WalkIn, now, barberId), now);

        var turnByTicket = turnRepository.GetByTicketNumber("B-010");
        turnRepository.MarkInService(turnId, barberId, now.AddMinutes(1));
        barberRepository.SetState(barberId, BarberState.InService, now.AddMinutes(1));

        Assert.Equal(turnId, turnByTicket?.Id);
        Assert.Equal(TurnState.InService, turnRepository.GetById(turnId)?.State);
        Assert.Equal(BarberState.InService, barberRepository.GetById(barberId)?.State);
    }

    [Fact]
    public void TurnRepository_EnforcesDisplayTicketNumberPerDay()
    {
        using var database = TestDatabase.Create();
        var repository = new LocalTurnRepository(database.Connection);
        var firstDay = DateTimeOffset.Parse("2026-06-03T14:30:00Z");
        var secondDay = firstDay.AddDays(1);

        repository.Upsert(CreateTurn(Guid.NewGuid(), "W20260603143000000", TurnState.Waiting, TurnSource.WalkIn, firstDay, displayTicketNumber: 1), firstDay);

        Assert.Throws<SqliteException>(() =>
            repository.Upsert(CreateTurn(Guid.NewGuid(), "W20260603143100000", TurnState.Waiting, TurnSource.WalkIn, firstDay.AddMinutes(1), displayTicketNumber: 1), firstDay));

        repository.Upsert(CreateTurn(Guid.NewGuid(), "W20260604143000000", TurnState.Waiting, TurnSource.WalkIn, secondDay, displayTicketNumber: 1), secondDay);

        Assert.Equal(2, repository.ListWaiting().Count);
    }

    [Fact]
    public void TurnRepository_ResolvesInternalQrOrVisibleTicketForToday()
    {
        using var database = TestDatabase.Create();
        var repository = new LocalTurnRepository(database.Connection);
        var today = DateTimeOffset.Parse("2026-06-03T14:30:00Z");
        var yesterday = today.AddDays(-1);
        var todayTurnId = Guid.NewGuid();

        repository.Upsert(CreateTurn(Guid.NewGuid(), "W20260602143000000", TurnState.Waiting, TurnSource.WalkIn, yesterday, displayTicketNumber: 1), yesterday);
        repository.Upsert(CreateTurn(todayTurnId, "W20260603143000000", TurnState.Waiting, TurnSource.WalkIn, today, displayTicketNumber: 1), today);

        Assert.Equal(todayTurnId, repository.GetByTicketInputForToday("1", today)?.Id);
        Assert.Equal(todayTurnId, repository.GetByTicketInputForToday("W20260603143000000", today)?.Id);
        Assert.Null(repository.GetByTicketInputForToday("999", today));
    }

    [Fact]
    public void LocalDatabaseInitializer_BackfillsDisplayTicketNumbersForExistingTurns()
    {
        using var database = TestDatabase.CreateUninitialized();
        using (var command = database.Connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE turns (
                    id TEXT NOT NULL PRIMARY KEY,
                    ticket_number TEXT NOT NULL UNIQUE,
                    state INTEGER NOT NULL,
                    source INTEGER NOT NULL,
                    customer_name TEXT NULL,
                    checked_in_at TEXT NOT NULL,
                    assigned_barber_id TEXT NULL,
                    appointment_id TEXT NULL,
                    requested_barber_ids TEXT NULL,
                    updated_at TEXT NOT NULL
                );

                INSERT INTO turns (id, ticket_number, state, source, customer_name, checked_in_at, assigned_barber_id, appointment_id, requested_barber_ids, updated_at)
                VALUES
                    ('11111111-1111-1111-1111-111111111111', 'W20260603100000000', 1, 0, 'Ana', '2026-06-03T10:00:00.0000000+00:00', NULL, NULL, NULL, '2026-06-03T10:00:00.0000000+00:00'),
                    ('22222222-2222-2222-2222-222222222222', 'W20260603100500000', 1, 0, 'Luis', '2026-06-03T10:05:00.0000000+00:00', NULL, NULL, NULL, '2026-06-03T10:05:00.0000000+00:00'),
                    ('33333333-3333-3333-3333-333333333333', 'W20260604100000000', 1, 0, 'Mia', '2026-06-04T10:00:00.0000000+00:00', NULL, NULL, NULL, '2026-06-04T10:00:00.0000000+00:00');
                """;
            command.ExecuteNonQuery();
        }

        LocalDatabaseInitializer.Initialize(database.Connection);

        var repository = new LocalTurnRepository(database.Connection);
        Assert.Equal(1, repository.GetByTicketNumber("W20260603100000000")?.DisplayTicketNumber);
        Assert.Equal(2, repository.GetByTicketNumber("W20260603100500000")?.DisplayTicketNumber);
        Assert.Equal(1, repository.GetByTicketNumber("W20260604100000000")?.DisplayTicketNumber);
    }

    [Fact]
    public void Transaction_CommitsCashBoxClosePersistenceAtomically()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Parse("2026-06-03T18:00:00Z");
        var barberId = Guid.NewGuid();
        var turnId = Guid.NewGuid();

        new LocalBarberRepository(database.Connection).Upsert(
            new Barber(barberId, "Luis", BarberState.InService, 2, 0, now.AddHours(-8), stationNumber: 1),
            now);
        new LocalTurnRepository(database.Connection).Upsert(
            CreateTurn(turnId, "A-010", TurnState.InService, TurnSource.WalkIn, now.AddMinutes(-30), barberId),
            now);

        var transaction = new LocalDataTransaction(database.ConnectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            new CashPaymentRepository(connection, sqliteTransaction).Add(
                new CashPayment(
                    Guid.NewGuid(),
                    turnId,
                    barberId,
                    2500,
                    "USD",
                    now,
                    "autocaja-1",
                    "R-001",
                    true,
                    500));
            new LocalTurnRepository(connection, sqliteTransaction).MarkCompleted(turnId, now);
            new LocalBarberRepository(connection, sqliteTransaction).ApplyCashBoxClose(barberId, BarberState.Available, 3, now);
            new AuditEventRepository(connection, sqliteTransaction).Add(
                new AuditEvent(Guid.NewGuid(), now, "cash_box_closed", "turn", turnId, """{"receipt":"R-001"}""", "autocaja-1"));
        });

        var savedTurn = new LocalTurnRepository(database.Connection).GetById(turnId);
        var savedBarber = new LocalBarberRepository(database.Connection).GetById(barberId);
        var payments = new CashPaymentRepository(database.Connection).ListByTurn(turnId);
        var auditEvents = new AuditEventRepository(database.Connection).ListAll();

        Assert.Equal(TurnState.Completed, savedTurn?.State);
        Assert.Equal(BarberState.Available, savedBarber?.State);
        Assert.Equal(3, savedBarber?.ClientsServedToday);
        Assert.Equal(3, savedBarber?.RotationOrder);
        Assert.Single(payments);
        Assert.Single(auditEvents);
    }

    [Fact]
    public void BarberRepository_PersistsCashBoxRotationQueue()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Parse("2026-06-04T18:00:00Z");
        var firstBarber = Guid.NewGuid();
        var closingBarber = Guid.NewGuid();
        var thirdBarber = Guid.NewGuid();
        var repository = new LocalBarberRepository(database.Connection);

        repository.Upsert(new Barber(firstBarber, "Ana", BarberState.Available, 1, 0, now, stationNumber: 1), now);
        repository.Upsert(new Barber(closingBarber, "Luis", BarberState.InService, 2, 1, now, stationNumber: 2), now);
        repository.Upsert(new Barber(thirdBarber, "Mia", BarberState.Available, 1, 2, now, stationNumber: 3), now);

        repository.SetRotationOrder(firstBarber, 0, now.AddMinutes(1));
        repository.SetRotationOrder(thirdBarber, 1, now.AddMinutes(1));
        repository.ApplyCashBoxClose(closingBarber, BarberState.Available, 2, now.AddMinutes(1));

        var barbers = repository.ListAll();

        Assert.Collection(
            barbers,
            barber => Assert.Equal(firstBarber, barber.Id),
            barber => Assert.Equal(thirdBarber, barber.Id),
            barber =>
            {
                Assert.Equal(closingBarber, barber.Id);
                Assert.Equal(BarberState.Available, barber.State);
                Assert.Equal(3, barber.ClientsServedToday);
            });
    }

    [Fact]
    public void Transaction_RollsBackWhenAnyLocalWriteFails()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Parse("2026-06-03T18:00:00Z");
        var barberId = Guid.NewGuid();
        var turnId = Guid.NewGuid();
        var duplicatedEventId = Guid.NewGuid();

        new LocalBarberRepository(database.Connection).Upsert(
            new Barber(barberId, "Luis", BarberState.InService, 2, 0, now.AddHours(-8), stationNumber: 1),
            now);
        new LocalTurnRepository(database.Connection).Upsert(
            CreateTurn(turnId, "A-010", TurnState.InService, TurnSource.WalkIn, now.AddMinutes(-30), barberId),
            now);
        new AuditEventRepository(database.Connection).Add(
            new AuditEvent(duplicatedEventId, now, "seed", "turn", turnId, "{}", "autocaja-1"));

        var transaction = new LocalDataTransaction(database.ConnectionFactory);

        Assert.Throws<SqliteException>(() =>
            transaction.Execute((connection, sqliteTransaction) =>
            {
                new CashPaymentRepository(connection, sqliteTransaction).Add(
                    new CashPayment(Guid.NewGuid(), turnId, barberId, 2500, "USD", now, "autocaja-1", null, true, 500));
                new LocalTurnRepository(connection, sqliteTransaction).MarkCompleted(turnId, now);
                new AuditEventRepository(connection, sqliteTransaction).Add(
                    new AuditEvent(duplicatedEventId, now, "duplicate", "turn", turnId, "{}", "autocaja-1"));
            }));

        var savedTurn = new LocalTurnRepository(database.Connection).GetById(turnId);
        var payments = new CashPaymentRepository(database.Connection).ListByTurn(turnId);

        Assert.Equal(TurnState.InService, savedTurn?.State);
        Assert.Empty(payments);
    }

    [Fact]
    public void AdminReportRepository_ReturnsDailyOperationsCashAndCommissions()
    {
        using var database = TestDatabase.Create();
        var from = DateTimeOffset.Parse("2026-06-04T00:00:00Z");
        var to = from.AddDays(1);
        var luisId = Guid.NewGuid();
        var anaId = Guid.NewGuid();
        var miaId = Guid.NewGuid();
        var luisTurnId = Guid.NewGuid();
        var anaTurnId = Guid.NewGuid();
        var waitingTurnId = Guid.NewGuid();
        var noShowTurnId = Guid.NewGuid();
        var oldTurnId = Guid.NewGuid();

        var barberRepository = new LocalBarberRepository(database.Connection);
        var turnRepository = new LocalTurnRepository(database.Connection);
        var paymentRepository = new CashPaymentRepository(database.Connection);

        barberRepository.Upsert(new Barber(luisId, "Luis", BarberState.Available, 3, 0, from, stationNumber: 1), from);
        barberRepository.Upsert(new Barber(anaId, "Ana", BarberState.Available, 1, 1, from, stationNumber: 2), from);
        barberRepository.Upsert(new Barber(miaId, "Mia", BarberState.Offline, 0, 2, from, isActive: false), from);

        turnRepository.Upsert(CreateTurn(luisTurnId, "A-001", TurnState.Completed, TurnSource.WalkIn, from.AddHours(9), luisId), from.AddHours(10));
        turnRepository.Upsert(CreateTurn(anaTurnId, "A-002", TurnState.Completed, TurnSource.Appointment, from.AddHours(9).AddMinutes(10), anaId), from.AddHours(10).AddMinutes(20));
        turnRepository.Upsert(CreateTurn(waitingTurnId, "A-003", TurnState.Waiting, TurnSource.WalkIn, from.AddHours(11)), from.AddHours(11));
        turnRepository.Upsert(CreateTurn(noShowTurnId, "A-004", TurnState.NoShow, TurnSource.WalkIn, from.AddHours(12)), from.AddHours(12));
        turnRepository.Upsert(CreateTurn(oldTurnId, "OLD-001", TurnState.Completed, TurnSource.WalkIn, from.AddDays(-1), luisId), from.AddDays(-1));

        paymentRepository.Add(new CashPayment(
            Guid.NewGuid(),
            luisTurnId,
            luisId,
            2500,
            "USD",
            from.AddHours(10),
            "autocaja-1",
            "R-001",
            true,
            500));
        paymentRepository.Add(new CashPayment(
            Guid.NewGuid(),
            anaTurnId,
            anaId,
            4000,
            "USD",
            from.AddHours(10).AddMinutes(20),
            "autocaja-1",
            "R-002",
            true,
            null));
        paymentRepository.Add(new CashPayment(
            Guid.NewGuid(),
            oldTurnId,
            luisId,
            9900,
            "USD",
            from.AddDays(-1).AddHours(18),
            "autocaja-1",
            "OLD",
            true,
            1980));

        var snapshot = new LocalAdminReportRepository(database.Connection)
            .Load(from, to, from.AddHours(13));

        Assert.Equal(4, snapshot.Operations.CheckIns);
        Assert.Equal(3, snapshot.Operations.WalkIns);
        Assert.Equal(1, snapshot.Operations.Appointments);
        Assert.Equal(2, snapshot.Operations.CompletedServices);
        Assert.Equal(1, snapshot.Operations.ActiveTurns);
        Assert.Equal(1, snapshot.Operations.NoShows);
        Assert.Equal(2, snapshot.Cash.PaymentCount);
        Assert.Equal(6500, snapshot.Cash.TotalAmountCents);
        Assert.Equal(500, snapshot.Cash.CommissionCents);
        Assert.Equal(1, snapshot.Cash.PaymentsMissingCommission);
        Assert.Equal(2, snapshot.Cash.CashDrawerOpenCount);

        Assert.Collection(
            snapshot.Barbers,
            row =>
            {
                Assert.Equal(anaId, row.BarberId);
                Assert.Equal(2, row.StationNumber);
                Assert.Equal(1, row.ServicesClosed);
                Assert.Equal(4000, row.CashCollectedCents);
                Assert.Equal(1, row.PaymentsMissingCommission);
            },
            row =>
            {
                Assert.Equal(luisId, row.BarberId);
                Assert.Equal(1, row.StationNumber);
                Assert.Equal(1, row.ServicesClosed);
                Assert.Equal(2500, row.CashCollectedCents);
                Assert.Equal(500, row.CommissionCents);
            },
            row =>
            {
                Assert.Equal(miaId, row.BarberId);
                Assert.Null(row.StationNumber);
                Assert.Equal(0, row.ServicesClosed);
                Assert.Equal(0, row.CashCollectedCents);
            });
        Assert.Collection(
            snapshot.RecentPayments,
            payment =>
            {
                Assert.Equal(2, payment.DisplayTicketNumber);
                Assert.Equal("A-002", payment.InternalTicketNumber);
                Assert.Equal("Ana", payment.BarberName);
                Assert.Equal(2, payment.BarberStationNumber);
                Assert.Null(payment.CommissionCents);
            },
            payment =>
            {
                Assert.Equal(1, payment.DisplayTicketNumber);
                Assert.Equal("A-001", payment.InternalTicketNumber);
                Assert.Equal("Luis", payment.BarberName);
                Assert.Equal(1, payment.BarberStationNumber);
                Assert.Equal(500, payment.CommissionCents);
            });
    }

    [Fact]
    public void AdminReportRepository_RejectsInvalidDateRange()
    {
        using var database = TestDatabase.Create();
        var from = DateTimeOffset.Parse("2026-06-04T00:00:00Z");

        Assert.Throws<ArgumentException>(() =>
            new LocalAdminReportRepository(database.Connection).Load(from, from, from));
    }

    private static Turn CreateTurn(
        Guid id,
        string ticketNumber,
        TurnState state,
        TurnSource source,
        DateTimeOffset checkedInAt,
        Guid? assignedBarberId = null,
        IReadOnlyCollection<Guid>? requestedBarberIds = null,
        string? customerName = null,
        int? displayTicketNumber = null)
    {
        return new Turn(
            id,
            ticketNumber,
            displayTicketNumber ?? ParseDisplayTicketNumber(ticketNumber),
            DateOnly.FromDateTime(checkedInAt.LocalDateTime),
            state,
            source,
            checkedInAt,
            assignedBarberId,
            requestedBarberIds: requestedBarberIds,
            customerName: customerName);
    }

    private static int ParseDisplayTicketNumber(string ticketNumber)
    {
        var suffix = ticketNumber.Split('-').LastOrDefault();
        return int.TryParse(suffix, out var number) && number > 0 ? number : 1;
    }
}
