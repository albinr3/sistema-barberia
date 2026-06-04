using Barberia.Data.Models;
using Microsoft.Data.Sqlite;

namespace Barberia.Data.Repositories;

public sealed class AuditEventRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;

    public AuditEventRepository(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public void Add(AuditEvent auditEvent)
    {
        if (auditEvent.Id == Guid.Empty || auditEvent.AggregateId == Guid.Empty)
        {
            throw new ArgumentException("Audit event and aggregate ids are required.", nameof(auditEvent));
        }

        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            INSERT INTO audit_events (
                id, occurred_at, event_type, aggregate_type, aggregate_id, device_id, payload
            ) VALUES (
                $id, $occurred_at, $event_type, $aggregate_type, $aggregate_id, $device_id, $payload
            );
            """;
        command.AddText("$id", auditEvent.Id.ToString());
        command.AddText("$occurred_at", auditEvent.OccurredAt.ToString("O"));
        command.AddText("$event_type", auditEvent.EventType);
        command.AddText("$aggregate_type", auditEvent.AggregateType);
        command.AddText("$aggregate_id", auditEvent.AggregateId.ToString());
        command.AddText("$device_id", auditEvent.DeviceId);
        command.AddText("$payload", auditEvent.Payload);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<AuditEvent> ListAll()
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, occurred_at, event_type, aggregate_type, aggregate_id, payload, device_id
            FROM audit_events
            ORDER BY occurred_at, event_type;
            """;

        using var reader = command.ExecuteReader();
        var events = new List<AuditEvent>();
        while (reader.Read())
        {
            events.Add(new AuditEvent(
                Guid.Parse(reader.GetString(0)),
                DateTimeOffset.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.GetString(3),
                Guid.Parse(reader.GetString(4)),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return events;
    }
}
