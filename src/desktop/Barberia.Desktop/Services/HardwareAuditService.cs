using System.Text.Json;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;

namespace Barberia.Desktop.Services;

internal sealed class HardwareAuditService
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public HardwareAuditService()
        : this(LocalDesktopDatabase.CreateConnectionFactory())
    {
    }

    public HardwareAuditService(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        new LocalDatabaseInitializer(_connectionFactory).Initialize();
    }

    public void Record(LanHardwareEventRequest request)
    {
        using var connection = _connectionFactory.OpenConnection();
        var now = OperationalClock.Now;
        new AuditEventRepository(connection).Add(new AuditEvent(
            Guid.NewGuid(),
            now,
            request.Succeeded ? "station_hardware_succeeded" : "station_hardware_failed",
            "hardware",
            Guid.NewGuid(),
            JsonSerializer.Serialize(new
            {
                request.StationRole,
                request.DeviceId,
                request.EventType,
                request.Succeeded,
                request.Message,
                request.ReceiptNumber,
                request.DisplayTicketNumber
            }),
            request.DeviceId));
    }
}
