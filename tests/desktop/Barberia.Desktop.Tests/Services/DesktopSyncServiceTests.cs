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
}
