using Microsoft.Data.Sqlite;

namespace Barberia.Data.Sync;

public sealed class SyncOutboxRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;

    public SyncOutboxRepository(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public void Add(SyncOutboxEvent outboxEvent)
    {
        Validate(outboxEvent);

        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            INSERT INTO sync_outbox_events (
                id, occurred_at, event_type, aggregate_type, aggregate_id, payload, device_id,
                created_at, state, attempt_count, next_attempt_at, last_attempted_at, synced_at, last_error
            ) VALUES (
                $id, $occurred_at, $event_type, $aggregate_type, $aggregate_id, $payload, $device_id,
                $created_at, $state, $attempt_count, $next_attempt_at, $last_attempted_at, $synced_at, $last_error
            );
            """;
        AddParameters(command, outboxEvent);
        command.ExecuteNonQuery();
    }

    public SyncOutboxEvent? GetById(Guid id)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, occurred_at, event_type, aggregate_type, aggregate_id, payload, device_id,
                   created_at, state, attempt_count, next_attempt_at, last_attempted_at, synced_at, last_error
            FROM sync_outbox_events
            WHERE id = $id;
            """;
        command.AddText("$id", id.ToString());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadOutboxEvent(reader) : null;
    }

    public IReadOnlyList<SyncOutboxEvent> ListReadyToSync(DateTimeOffset now, int limit)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        }

        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, occurred_at, event_type, aggregate_type, aggregate_id, payload, device_id,
                   created_at, state, attempt_count, next_attempt_at, last_attempted_at, synced_at, last_error
            FROM sync_outbox_events
            WHERE state = $pending
              AND (next_attempt_at IS NULL OR next_attempt_at <= $now)
            ORDER BY occurred_at, created_at
            LIMIT $limit;
            """;
        command.AddInteger("$pending", (int)SyncOutboxEventState.Pending);
        command.AddText("$now", now.ToString("O"));
        command.AddInteger("$limit", limit);

        return ReadAll(command);
    }

    public IReadOnlyList<SyncOutboxEvent> ListAll()
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, occurred_at, event_type, aggregate_type, aggregate_id, payload, device_id,
                   created_at, state, attempt_count, next_attempt_at, last_attempted_at, synced_at, last_error
            FROM sync_outbox_events
            ORDER BY occurred_at, created_at;
            """;

        return ReadAll(command);
    }

    public void MarkSynced(Guid id, DateTimeOffset syncedAt)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            UPDATE sync_outbox_events
            SET state = $state,
                last_attempted_at = $last_attempted_at,
                synced_at = $synced_at,
                last_error = NULL
            WHERE id = $id;
            """;
        command.AddText("$id", id.ToString());
        command.AddInteger("$state", (int)SyncOutboxEventState.Synced);
        command.AddText("$last_attempted_at", syncedAt.ToString("O"));
        command.AddText("$synced_at", syncedAt.ToString("O"));

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("Sync outbox event was not found for success update.");
        }
    }

    public void MarkAttemptFailed(Guid id, DateTimeOffset attemptedAt, DateTimeOffset nextAttemptAt, string errorMessage)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            UPDATE sync_outbox_events
            SET state = $state,
                attempt_count = attempt_count + 1,
                next_attempt_at = $next_attempt_at,
                last_attempted_at = $last_attempted_at,
                last_error = $last_error
            WHERE id = $id;
            """;
        command.AddText("$id", id.ToString());
        command.AddInteger("$state", (int)SyncOutboxEventState.Pending);
        command.AddText("$next_attempt_at", nextAttemptAt.ToString("O"));
        command.AddText("$last_attempted_at", attemptedAt.ToString("O"));
        command.AddText("$last_error", string.IsNullOrWhiteSpace(errorMessage) ? "Cloud sync failed." : errorMessage.Trim());

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("Sync outbox event was not found for failure update.");
        }
    }

    private static void AddParameters(SqliteCommand command, SyncOutboxEvent outboxEvent)
    {
        command.AddText("$id", outboxEvent.Id.ToString());
        command.AddText("$occurred_at", outboxEvent.OccurredAt.ToString("O"));
        command.AddText("$event_type", outboxEvent.EventType.Trim());
        command.AddText("$aggregate_type", outboxEvent.AggregateType.Trim());
        command.AddText("$aggregate_id", outboxEvent.AggregateId.ToString());
        command.AddText("$payload", outboxEvent.Payload);
        command.AddText("$device_id", outboxEvent.DeviceId);
        command.AddText("$created_at", outboxEvent.CreatedAt.ToString("O"));
        command.AddInteger("$state", (int)outboxEvent.State);
        command.AddInteger("$attempt_count", outboxEvent.AttemptCount);
        command.AddText("$next_attempt_at", Format(outboxEvent.NextAttemptAt));
        command.AddText("$last_attempted_at", Format(outboxEvent.LastAttemptedAt));
        command.AddText("$synced_at", Format(outboxEvent.SyncedAt));
        command.AddText("$last_error", outboxEvent.LastError);
    }

    private static IReadOnlyList<SyncOutboxEvent> ReadAll(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var events = new List<SyncOutboxEvent>();
        while (reader.Read())
        {
            events.Add(ReadOutboxEvent(reader));
        }

        return events;
    }

    private static SyncOutboxEvent ReadOutboxEvent(SqliteDataReader reader)
    {
        return new SyncOutboxEvent(
            Guid.Parse(reader.GetString(0)),
            DateTimeOffset.Parse(reader.GetString(1)),
            reader.GetString(2),
            reader.GetString(3),
            Guid.Parse(reader.GetString(4)),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            DateTimeOffset.Parse(reader.GetString(7)),
            (SyncOutboxEventState)reader.GetInt32(8),
            reader.GetInt32(9),
            ReadOptionalDateTime(reader, 10),
            ReadOptionalDateTime(reader, 11),
            ReadOptionalDateTime(reader, 12),
            reader.IsDBNull(13) ? null : reader.GetString(13));
    }

    private static DateTimeOffset? ReadOptionalDateTime(SqliteDataReader reader, int index)
    {
        return reader.IsDBNull(index) ? null : DateTimeOffset.Parse(reader.GetString(index));
    }

    private static string? Format(DateTimeOffset? value)
    {
        return value?.ToString("O");
    }

    private static void Validate(SyncOutboxEvent outboxEvent)
    {
        if (outboxEvent.Id == Guid.Empty || outboxEvent.AggregateId == Guid.Empty)
        {
            throw new ArgumentException("Sync outbox event and aggregate ids are required.", nameof(outboxEvent));
        }

        if (string.IsNullOrWhiteSpace(outboxEvent.EventType))
        {
            throw new ArgumentException("Sync event type is required.", nameof(outboxEvent));
        }

        if (string.IsNullOrWhiteSpace(outboxEvent.AggregateType))
        {
            throw new ArgumentException("Sync aggregate type is required.", nameof(outboxEvent));
        }

        if (string.IsNullOrWhiteSpace(outboxEvent.Payload))
        {
            throw new ArgumentException("Sync event payload is required.", nameof(outboxEvent));
        }

        if (outboxEvent.AttemptCount < 0)
        {
            throw new ArgumentException("Sync attempt count cannot be negative.", nameof(outboxEvent));
        }
    }
}
