using Barberia.Core.Domain;
using Microsoft.Data.Sqlite;

namespace Barberia.Data.Repositories;

public sealed class LocalTurnRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;

    public LocalTurnRepository(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public void Upsert(Turn turn, DateTimeOffset updatedAt)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            INSERT INTO turns (
                id, ticket_number, state, source, checked_in_at, assigned_barber_id,
                appointment_id, requested_barber_ids, updated_at
            ) VALUES (
                $id, $ticket_number, $state, $source, $checked_in_at, $assigned_barber_id,
                $appointment_id, $requested_barber_ids, $updated_at
            )
            ON CONFLICT(id) DO UPDATE SET
                ticket_number = excluded.ticket_number,
                state = excluded.state,
                source = excluded.source,
                checked_in_at = excluded.checked_in_at,
                assigned_barber_id = excluded.assigned_barber_id,
                appointment_id = excluded.appointment_id,
                requested_barber_ids = excluded.requested_barber_ids,
                updated_at = excluded.updated_at;
            """;
        command.AddText("$id", turn.Id.ToString());
        command.AddText("$ticket_number", turn.TicketNumber);
        command.AddInteger("$state", (int)turn.State);
        command.AddInteger("$source", (int)turn.Source);
        command.AddText("$checked_in_at", turn.CheckedInAt.ToString("O"));
        command.AddText("$assigned_barber_id", turn.AssignedBarberId?.ToString());
        command.AddText("$appointment_id", turn.AppointmentId?.ToString());
        command.AddText("$requested_barber_ids", FormatIds(turn.RequestedBarberIds));
        command.AddText("$updated_at", updatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    public Turn? GetById(Guid id)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, ticket_number, state, source, checked_in_at, assigned_barber_id,
                   appointment_id, requested_barber_ids
            FROM turns
            WHERE id = $id;
            """;
        command.AddText("$id", id.ToString());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadTurn(reader) : null;
    }

    public IReadOnlyList<Turn> ListWaiting()
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, ticket_number, state, source, checked_in_at, assigned_barber_id,
                   appointment_id, requested_barber_ids
            FROM turns
            WHERE state = $state
            ORDER BY checked_in_at, ticket_number;
            """;
        command.AddInteger("$state", (int)TurnState.Waiting);

        using var reader = command.ExecuteReader();
        var turns = new List<Turn>();
        while (reader.Read())
        {
            turns.Add(ReadTurn(reader));
        }

        return turns;
    }

    public void ApplyAssignment(Guid turnId, Guid barberId, TurnState state, DateTimeOffset updatedAt)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            UPDATE turns
            SET state = $state,
                assigned_barber_id = $barber_id,
                updated_at = $updated_at
            WHERE id = $turn_id;
            """;
        command.AddText("$turn_id", turnId.ToString());
        command.AddText("$barber_id", barberId.ToString());
        command.AddInteger("$state", (int)state);
        command.AddText("$updated_at", updatedAt.ToString("O"));

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("Turn was not found for assignment.");
        }
    }

    public void MarkCompleted(Guid turnId, DateTimeOffset updatedAt)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            UPDATE turns
            SET state = $state,
                updated_at = $updated_at
            WHERE id = $turn_id;
            """;
        command.AddText("$turn_id", turnId.ToString());
        command.AddInteger("$state", (int)TurnState.Completed);
        command.AddText("$updated_at", updatedAt.ToString("O"));

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("Turn was not found for completion.");
        }
    }

    private static Turn ReadTurn(SqliteDataReader reader)
    {
        return new Turn(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            (TurnState)reader.GetInt32(2),
            (TurnSource)reader.GetInt32(3),
            DateTimeOffset.Parse(reader.GetString(4)),
            reader.IsDBNull(5) ? null : Guid.Parse(reader.GetString(5)),
            reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6)),
            ParseIds(reader.IsDBNull(7) ? null : reader.GetString(7)));
    }

    private static string? FormatIds(IReadOnlyCollection<Guid>? ids)
    {
        return ids is null || ids.Count == 0 ? null : string.Join(",", ids.Select(id => id.ToString()));
    }

    private static IReadOnlyCollection<Guid>? ParseIds(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToArray();
    }
}
