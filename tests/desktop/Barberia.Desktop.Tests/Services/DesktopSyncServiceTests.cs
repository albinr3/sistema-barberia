using System;
using System.Reflection;
using System.Text.Json;
using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Barberia.Data.Sync;
using Barberia.Desktop.Services;
using Barberia.Sync.Outbox;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Barberia.Desktop.Tests.Services;

public class DesktopSyncServiceTests : IDisposable
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqliteConnection _keepAliveConnection;

    public DesktopSyncServiceTests()
    {
        var connectionString = $"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connectionFactory = new SqliteConnectionFactory(connectionString);
        _keepAliveConnection = _connectionFactory.OpenConnection();
        new LocalDatabaseInitializer(_connectionFactory).Initialize();
    }

    public void Dispose()
    {
        _keepAliveConnection.Dispose();
    }

    private void InvokeApplyTicketCommand(JsonElement command, DateTimeOffset now, DesktopSyncSettings settings)
    {
        var service = new DesktopSyncService(_connectionFactory);
        var method = typeof(DesktopSyncService).GetMethod("ApplyTicketCommand", BindingFlags.NonPublic | BindingFlags.Instance);
        var localAdminService = new LocalAdminService(_connectionFactory);
        method!.Invoke(service, new object[] { settings, command, localAdminService, now });
    }

    private void InvokeApplyPayrollCommand(JsonElement command, DateTimeOffset now, DesktopSyncSettings settings)
    {
        var service = new DesktopSyncService(_connectionFactory);
        var method = typeof(DesktopSyncService).GetMethod("ApplyPayrollCommand", BindingFlags.NonPublic | BindingFlags.Instance);
        method!.Invoke(service, new object[] { settings, command, now });
    }

    [Fact]
    public void ApplyTicketCommand_Success_ReassignsTurnAndEnqueuesAck()
    {
        // Setup
        var now = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var ticketId = Guid.NewGuid();
        var targetBarberId = Guid.NewGuid();
        var commandId = Guid.NewGuid();

        using (var connection = _connectionFactory.OpenConnection())
        {
            var barberRepo = new LocalBarberRepository(connection);
            barberRepo.Upsert(new Barber(targetBarberId, "Barber 1", BarberState.Available, 0, 1, now, 1, null, true, 50), now);

            var turnRepo = new LocalTurnRepository(connection);
            var turn = new Turn(ticketId, "T1", 1, DateOnly.FromDateTime(now.Date), TurnState.Waiting, TurnSource.WalkIn, now, null, null, Array.Empty<Guid>(), "Customer 1", null, null, null);
            turnRepo.Upsert(turn, now);
        }

        var json = $$"""
        {
            "type": "ticket.reassign",
            "data": {
                "id": "{{commandId}}",
                "local_ticket_id": "{{ticketId}}",
                "target_barber_id": "{{targetBarberId}}"
            }
        }
        """;
        var document = JsonDocument.Parse(json);
        var settings = new DesktopSyncSettings("http://test", "test-device", "secret", 60);

        // Act
        InvokeApplyTicketCommand(document.RootElement, now, settings);

        // Assert
        using (var connection = _connectionFactory.OpenConnection())
        {
            var turnRepo = new LocalTurnRepository(connection);
            var turn = turnRepo.GetById(ticketId);
            Assert.Equal(TurnState.Called, turn!.State);
            Assert.Equal(targetBarberId, turn.AssignedBarberId);

            var syncStateRepo = new SyncStateRepository(connection);
            Assert.Equal("processed", syncStateRepo.GetValue($"cloud_ticket_command:{commandId}"));

            var outboxRepo = new SyncOutboxRepository(connection);
            var events = outboxRepo.ListAll();
            var ackEvent = Assert.Single(events, e => e.EventType == "ticket_admin_command.applied");
            Assert.Contains(commandId.ToString(), ackEvent.Payload);

            var calledEvent = Assert.Single(events, e => e.EventType == "ticket.called" && e.AggregateId == ticketId);
            Assert.Contains(targetBarberId.ToString(), calledEvent.Payload);
            Assert.Contains("\"status\":\"called\"", calledEvent.Payload);
        }
    }

    [Fact]
    public void ApplyTicketCommand_TurnNotFound_EnqueuesFailedAck()
    {
        // Setup
        var now = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var targetBarberId = Guid.NewGuid();
        var commandId = Guid.NewGuid();

        using (var connection = _connectionFactory.OpenConnection())
        {
            var barberRepo = new LocalBarberRepository(connection);
            barberRepo.Upsert(new Barber(targetBarberId, "Barber 1", BarberState.Available, 0, 1, now, 1, null, true, 50), now);
        }

        var json = $$"""
        {
            "type": "ticket.reassign",
            "data": {
                "id": "{{commandId}}",
                "local_ticket_id": "{{Guid.NewGuid()}}",
                "target_barber_id": "{{targetBarberId}}"
            }
        }
        """;
        var document = JsonDocument.Parse(json);
        var settings = new DesktopSyncSettings("http://test", "test-device", "secret", 60);

        // Act
        InvokeApplyTicketCommand(document.RootElement, now, settings);

        // Assert
        using (var connection = _connectionFactory.OpenConnection())
        {
            var outboxRepo = new SyncOutboxRepository(connection);
            var events = outboxRepo.ListAll();
            var ackEvent = Assert.Single(events, e => e.EventType == "ticket_admin_command.failed");
            Assert.Contains(commandId.ToString(), ackEvent.Payload);
            Assert.Contains("error_message", ackEvent.Payload);

            var syncStateRepo = new SyncStateRepository(connection);
            Assert.Equal("processed", syncStateRepo.GetValue($"cloud_ticket_command:{commandId}"));
        }
    }

    [Fact]
    public void ApplyTicketCommand_Idempotency_SkipsAlreadyProcessed()
    {
        // Setup
        var now = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var commandId = Guid.NewGuid();

        using (var connection = _connectionFactory.OpenConnection())
        {
            var syncStateRepo = new SyncStateRepository(connection);
            syncStateRepo.SetValue($"cloud_ticket_command:{commandId}", "processed", now);
        }

        var json = $$"""
        {
            "type": "ticket.reassign",
            "data": {
                "id": "{{commandId}}",
                "local_ticket_id": "{{Guid.NewGuid()}}",
                "target_barber_id": "{{Guid.NewGuid()}}"
            }
        }
        """;
        var document = JsonDocument.Parse(json);
        var settings = new DesktopSyncSettings("http://test", "test-device", "secret", 60);

        // Act
        InvokeApplyTicketCommand(document.RootElement, now, settings);

        // Assert
        using (var connection = _connectionFactory.OpenConnection())
        {
            var outboxRepo = new SyncOutboxRepository(connection);
            var events = outboxRepo.ListAll();
            Assert.Empty(events);
        }
    }

    [Fact]
    public void ApplyPayrollCommand_AdjustmentAdded_StoresPendingAdjustmentAndEnqueuesSnapshotAck()
    {
        var now = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var friday = new DateTimeOffset(new DateTime(2026, 6, 12), OperationalClock.Now.Offset);
        var barberId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var settings = new DesktopSyncSettings("http://test", Guid.NewGuid().ToString(), "secret", 60);

        using (var connection = _connectionFactory.OpenConnection())
        {
            new LocalBarberRepository(connection).Upsert(
                new Barber(barberId, "Ana", BarberState.Available, 0, 0, now, stationNumber: 1),
                now);
        }

        var json = $$"""
        {
            "type": "payroll.adjustment_added",
            "data": {
                "id": "{{commandId}}",
                "start_date": "2026-06-12",
                "end_date": "2026-06-19",
                "payload": {
                    "barber_id": "{{barberId}}",
                    "amount_cents": -500,
                    "reason": "Correction"
                }
            }
        }
        """;

        using var document = JsonDocument.Parse(json);
        InvokeApplyPayrollCommand(document.RootElement, now, settings);

        using (var connection = _connectionFactory.OpenConnection())
        {
            var pending = new PayrollPendingAdjustmentRepository(connection).ListByRange(friday, friday.AddDays(7));
            var adjustment = Assert.Single(pending);
            Assert.Equal(commandId, adjustment.CommandId);
            Assert.Equal(-500, adjustment.AmountCents);

            var outboxEvents = new SyncOutboxRepository(connection).ListAll();
            Assert.Contains(outboxEvents, e => e.EventType == "payroll.snapshot");
            Assert.Contains(outboxEvents, e => e.EventType == "payroll_admin_command.applied" && e.AggregateId == commandId);
        }
    }

    [Fact]
    public void ApplyPayrollCommand_PayRequest_FailsWhenOutboxHasPendingEvents()
    {
        var now = new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);
        var commandId = Guid.NewGuid();
        var settings = new DesktopSyncSettings("http://test", Guid.NewGuid().ToString(), "secret", 60);

        using (var connection = _connectionFactory.OpenConnection())
        {
            new SyncOutboxRecorder(new SyncOutboxRepository(connection)).Enqueue(
                new LocalSyncEvent(Guid.NewGuid(), now, "ticket.completed", "ticket", Guid.NewGuid(), """{"status":"completed"}"""),
                now);
        }

        var json = $$"""
        {
            "type": "payroll.pay_requested",
            "data": {
                "id": "{{commandId}}",
                "start_date": "2026-06-12",
                "end_date": "2026-06-19",
                "payload": {
                    "payment_method": "cash"
                }
            }
        }
        """;

        using var document = JsonDocument.Parse(json);
        InvokeApplyPayrollCommand(document.RootElement, now, settings);

        using (var connection = _connectionFactory.OpenConnection())
        {
            var outboxEvents = new SyncOutboxRepository(connection).ListAll();
            Assert.Contains(outboxEvents, e => e.EventType == "payroll_admin_command.failed" && e.AggregateId == commandId);
        }
    }

    [Fact]
    public void ApplyPayrollCommand_PayRequest_PaysAndClearsPendingAdjustments()
    {
        var friday = new DateTimeOffset(new DateTime(2026, 6, 12), OperationalClock.Now.Offset);
        var now = friday.AddDays(7).AddHours(12);
        var barberId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var settings = new DesktopSyncSettings("http://test", Guid.NewGuid().ToString(), "secret", 60);

        SeedBarberTurnAndPayment(barberId, "Ana", 1, friday.AddHours(10), 2500, 1600);
        using (var connection = _connectionFactory.OpenConnection())
        {
            new PayrollPendingAdjustmentRepository(connection).Add(
                new PayrollPendingAdjustment(Guid.NewGuid(), Guid.NewGuid(), friday, friday.AddDays(7), barberId, 200, "Bonus", friday.AddDays(1)));
        }

        var json = $$"""
        {
            "type": "payroll.pay_requested",
            "data": {
                "id": "{{commandId}}",
                "start_date": "2026-06-12",
                "end_date": "2026-06-19",
                "payload": {
                    "payment_method": "cash",
                    "payment_reference": "WEB-1"
                }
            }
        }
        """;

        using var document = JsonDocument.Parse(json);
        InvokeApplyPayrollCommand(document.RootElement, now, settings);

        using (var connection = _connectionFactory.OpenConnection())
        {
            var period = new PayrollRepository(connection).GetPeriodByDates(friday, friday.AddDays(7));
            Assert.NotNull(period);
            Assert.Equal(PayrollPeriodState.Paid, period.State);
            Assert.Equal("WEB-1", period.PaymentReference);
            Assert.Empty(new PayrollPendingAdjustmentRepository(connection).ListByRange(friday, friday.AddDays(7)));

            var outboxEvents = new SyncOutboxRepository(connection).ListAll();
            Assert.Contains(outboxEvents, e => e.EventType == "payroll.snapshot");
            Assert.Contains(outboxEvents, e => e.EventType == "payroll_admin_command.applied" && e.AggregateId == commandId);
        }
    }

    private void SeedBarberTurnAndPayment(
        Guid barberId,
        string barberName,
        int stationNumber,
        DateTimeOffset collectedAt,
        long amountCents,
        long? commissionCents)
    {
        using var connection = _connectionFactory.OpenConnection();
        var barberRepository = new LocalBarberRepository(connection);
        if (barberRepository.GetById(barberId) is null)
        {
            barberRepository.Upsert(new Barber(barberId, barberName, BarberState.Available, 0, 0, collectedAt, stationNumber: stationNumber), collectedAt);
        }

        var turnId = Guid.NewGuid();
        new LocalTurnRepository(connection).Upsert(
            new Turn(
                turnId,
                $"T-{Guid.NewGuid():N}",
                Random.Shared.Next(1, 1_000_000),
                DateOnly.FromDateTime(collectedAt.LocalDateTime),
                TurnState.Completed,
                TurnSource.WalkIn,
                collectedAt,
                barberId),
            collectedAt);
        new CashPaymentRepository(connection).Add(new CashPayment(
            Guid.NewGuid(),
            turnId,
            barberId,
            amountCents,
            "USD",
            collectedAt,
            "autocaja-1",
            null,
            true,
            commissionCents));
    }
}
