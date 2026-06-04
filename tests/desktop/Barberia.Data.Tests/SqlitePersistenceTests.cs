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
        barberRepository.Upsert(new Barber(barberId, " Luis ", BarberState.Available, 0, 1, now), now);
        barberRepository.Upsert(new Barber(requestedBarberId, "Ana", BarberState.Available, 1, 2, now), now);

        var turnRepository = new LocalTurnRepository(database.Connection);
        var laterTurn = new Turn(
            Guid.NewGuid(),
            "A-002",
            TurnState.Waiting,
            TurnSource.WalkIn,
            now.AddMinutes(2));
        var firstTurn = new Turn(
            Guid.NewGuid(),
            "A-001",
            TurnState.Waiting,
            TurnSource.WalkIn,
            now,
            requestedBarberIds: [requestedBarberId]);

        turnRepository.Upsert(laterTurn, now);
        turnRepository.Upsert(firstTurn, now);

        var savedBarber = barberRepository.GetById(barberId);
        var waitingTurns = turnRepository.ListWaiting();

        Assert.NotNull(savedBarber);
        Assert.Equal("Luis", savedBarber.DisplayName);
        Assert.Equal(BarberState.Available, savedBarber.State);
        Assert.Collection(
            waitingTurns,
            turn =>
            {
                Assert.Equal(firstTurn.Id, turn.Id);
                Assert.Equal([requestedBarberId], turn.RequestedBarberIds);
            },
            turn => Assert.Equal(laterTurn.Id, turn.Id));
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
            new Barber(barberId, "Ana", BarberState.Available, 0, 0, now),
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
        var called = new Turn(Guid.NewGuid(), "A-002", TurnState.Called, TurnSource.WalkIn, now.AddMinutes(2));
        var assigned = new Turn(Guid.NewGuid(), "A-001", TurnState.Assigned, TurnSource.Appointment, now);
        var waiting = new Turn(Guid.NewGuid(), "A-003", TurnState.Waiting, TurnSource.WalkIn, now.AddMinutes(3));
        var completed = new Turn(Guid.NewGuid(), "A-004", TurnState.Completed, TurnSource.WalkIn, now.AddMinutes(4));

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

        barberRepository.Upsert(new Barber(barberId, "Luis", BarberState.Called, 0, 0, now), now);
        barberRepository.Upsert(new Barber(otherBarberId, "Ana", BarberState.Called, 0, 1, now), now);

        repository.Upsert(new Turn(Guid.NewGuid(), "B-001", TurnState.Assigned, TurnSource.WalkIn, now, barberId), now);
        repository.Upsert(new Turn(Guid.NewGuid(), "B-002", TurnState.Called, TurnSource.WalkIn, now.AddMinutes(1), barberId), now);
        repository.Upsert(new Turn(Guid.NewGuid(), "B-003", TurnState.InService, TurnSource.WalkIn, now.AddMinutes(2), barberId), now);
        repository.Upsert(new Turn(Guid.NewGuid(), "B-004", TurnState.Assigned, TurnSource.WalkIn, now.AddMinutes(3), otherBarberId), now);

        var turns = repository.ListAssignedToBarber(barberId);

        Assert.Collection(
            turns,
            turn => Assert.Equal("B-001", turn.TicketNumber),
            turn => Assert.Equal("B-002", turn.TicketNumber));
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
        barberRepository.Upsert(new Barber(barberId, "Ana", BarberState.Called, 0, 0, now), now);
        turnRepository.Upsert(new Turn(turnId, "B-010", TurnState.Assigned, TurnSource.WalkIn, now, barberId), now);

        var turnByTicket = turnRepository.GetByTicketNumber("B-010");
        turnRepository.MarkInService(turnId, barberId, now.AddMinutes(1));
        barberRepository.SetState(barberId, BarberState.InService, now.AddMinutes(1));

        Assert.Equal(turnId, turnByTicket?.Id);
        Assert.Equal(TurnState.InService, turnRepository.GetById(turnId)?.State);
        Assert.Equal(BarberState.InService, barberRepository.GetById(barberId)?.State);
    }

    [Fact]
    public void Transaction_CommitsCashBoxClosePersistenceAtomically()
    {
        using var database = TestDatabase.Create();
        var now = DateTimeOffset.Parse("2026-06-03T18:00:00Z");
        var barberId = Guid.NewGuid();
        var turnId = Guid.NewGuid();

        new LocalBarberRepository(database.Connection).Upsert(
            new Barber(barberId, "Luis", BarberState.InService, 2, 0, now.AddHours(-8)),
            now);
        new LocalTurnRepository(database.Connection).Upsert(
            new Turn(turnId, "A-010", TurnState.InService, TurnSource.WalkIn, now.AddMinutes(-30), barberId),
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

        repository.Upsert(new Barber(firstBarber, "Ana", BarberState.Available, 1, 0, now), now);
        repository.Upsert(new Barber(closingBarber, "Luis", BarberState.InService, 2, 1, now), now);
        repository.Upsert(new Barber(thirdBarber, "Mia", BarberState.Available, 1, 2, now), now);

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
            new Barber(barberId, "Luis", BarberState.InService, 2, 0, now.AddHours(-8)),
            now);
        new LocalTurnRepository(database.Connection).Upsert(
            new Turn(turnId, "A-010", TurnState.InService, TurnSource.WalkIn, now.AddMinutes(-30), barberId),
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

        barberRepository.Upsert(new Barber(luisId, "Luis", BarberState.Available, 3, 0, from), from);
        barberRepository.Upsert(new Barber(anaId, "Ana", BarberState.Available, 1, 1, from), from);
        barberRepository.Upsert(new Barber(miaId, "Mia", BarberState.Offline, 0, 2, from), from);

        turnRepository.Upsert(new Turn(luisTurnId, "A-001", TurnState.Completed, TurnSource.WalkIn, from.AddHours(9), luisId), from.AddHours(10));
        turnRepository.Upsert(new Turn(anaTurnId, "A-002", TurnState.Completed, TurnSource.Appointment, from.AddHours(9).AddMinutes(10), anaId), from.AddHours(10).AddMinutes(20));
        turnRepository.Upsert(new Turn(waitingTurnId, "A-003", TurnState.Waiting, TurnSource.WalkIn, from.AddHours(11)), from.AddHours(11));
        turnRepository.Upsert(new Turn(noShowTurnId, "A-004", TurnState.NoShow, TurnSource.WalkIn, from.AddHours(12)), from.AddHours(12));
        turnRepository.Upsert(new Turn(oldTurnId, "OLD-001", TurnState.Completed, TurnSource.WalkIn, from.AddDays(-1), luisId), from.AddDays(-1));

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
                Assert.Equal(1, row.ServicesClosed);
                Assert.Equal(4000, row.CashCollectedCents);
                Assert.Equal(1, row.PaymentsMissingCommission);
            },
            row =>
            {
                Assert.Equal(luisId, row.BarberId);
                Assert.Equal(1, row.ServicesClosed);
                Assert.Equal(2500, row.CashCollectedCents);
                Assert.Equal(500, row.CommissionCents);
            },
            row =>
            {
                Assert.Equal(miaId, row.BarberId);
                Assert.Equal(0, row.ServicesClosed);
                Assert.Equal(0, row.CashCollectedCents);
            });
        Assert.Collection(
            snapshot.RecentPayments,
            payment =>
            {
                Assert.Equal("A-002", payment.TicketNumber);
                Assert.Equal("Ana", payment.BarberName);
                Assert.Null(payment.CommissionCents);
            },
            payment =>
            {
                Assert.Equal("A-001", payment.TicketNumber);
                Assert.Equal("Luis", payment.BarberName);
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
}
