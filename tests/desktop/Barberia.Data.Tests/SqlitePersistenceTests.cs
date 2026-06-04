using Barberia.Core.Domain;
using Barberia.Data.Models;
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
}
