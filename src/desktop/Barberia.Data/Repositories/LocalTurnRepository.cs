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
                id, ticket_number, display_ticket_number, ticket_date, state, source, customer_name, checked_in_at, assigned_barber_id,
                appointment_id, requested_barber_ids, updated_at
            ) VALUES (
                $id, $ticket_number, $display_ticket_number, $ticket_date, $state, $source, $customer_name, $checked_in_at, $assigned_barber_id,
                $appointment_id, $requested_barber_ids, $updated_at
            )
            ON CONFLICT(id) DO UPDATE SET
                ticket_number = excluded.ticket_number,
                display_ticket_number = excluded.display_ticket_number,
                ticket_date = excluded.ticket_date,
                state = excluded.state,
                source = excluded.source,
                customer_name = excluded.customer_name,
                checked_in_at = excluded.checked_in_at,
                assigned_barber_id = excluded.assigned_barber_id,
                appointment_id = excluded.appointment_id,
                requested_barber_ids = excluded.requested_barber_ids,
                updated_at = excluded.updated_at;
            """;
        command.AddText("$id", turn.Id.ToString());
        command.AddText("$ticket_number", turn.TicketNumber);
        command.AddInteger("$display_ticket_number", turn.DisplayTicketNumber);
        command.AddText("$ticket_date", FormatDate(turn.TicketDate));
        command.AddInteger("$state", (int)turn.State);
        command.AddInteger("$source", (int)turn.Source);
        command.AddText("$customer_name", turn.CustomerName);
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
            SELECT id, ticket_number, display_ticket_number, ticket_date, state, source, checked_in_at, assigned_barber_id,
                   appointment_id, requested_barber_ids, customer_name
            FROM turns
            WHERE id = $id;
            """;
        command.AddText("$id", id.ToString());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadTurn(reader) : null;
    }

    public Turn? GetByTicketNumber(string ticketNumber)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, ticket_number, display_ticket_number, ticket_date, state, source, checked_in_at, assigned_barber_id,
                   appointment_id, requested_barber_ids, customer_name
            FROM turns
            WHERE ticket_number = $ticket_number;
            """;
        command.AddText("$ticket_number", ticketNumber.Trim());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadTurn(reader) : null;
    }

    public Turn? GetByTicketInputForToday(string ticketInput, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(ticketInput))
        {
            return null;
        }

        var normalized = ticketInput.Trim();
        if (normalized.StartsWith("W", StringComparison.OrdinalIgnoreCase))
        {
            return GetByTicketNumber(normalized);
        }

        if (!int.TryParse(normalized, out var displayTicketNumber) || displayTicketNumber <= 0)
        {
            return null;
        }

        return GetByDisplayTicketNumber(DateOnly.FromDateTime(now.LocalDateTime), displayTicketNumber);
    }

    public Turn? GetByDisplayTicketNumber(DateOnly ticketDate, int displayTicketNumber)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, ticket_number, display_ticket_number, ticket_date, state, source, checked_in_at, assigned_barber_id,
                   appointment_id, requested_barber_ids, customer_name
            FROM turns
            WHERE ticket_date = $ticket_date
              AND display_ticket_number = $display_ticket_number;
            """;
        command.AddText("$ticket_date", FormatDate(ticketDate));
        command.AddInteger("$display_ticket_number", displayTicketNumber);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadTurn(reader) : null;
    }

    public int GetNextDisplayTicketNumber(DateOnly ticketDate)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT COALESCE(MAX(display_ticket_number), 0) + 1
            FROM turns
            WHERE ticket_date = $ticket_date;
            """;
        command.AddText("$ticket_date", FormatDate(ticketDate));
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public IReadOnlyList<Turn> ListWaiting()
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, ticket_number, display_ticket_number, ticket_date, state, source, checked_in_at, assigned_barber_id,
                   appointment_id, requested_barber_ids, customer_name
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

    public IReadOnlyList<Turn> ListActiveForPublicDisplay()
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, ticket_number, display_ticket_number, ticket_date, state, source, checked_in_at, assigned_barber_id,
                   appointment_id, requested_barber_ids, customer_name
            FROM turns
            WHERE state IN ($waiting, $called, $in_service)
            ORDER BY
                CASE state
                    WHEN $called THEN 0
                    WHEN $in_service THEN 2
                    ELSE 3
                END,
                checked_in_at,
                ticket_number;
            """;
        command.AddInteger("$waiting", (int)TurnState.Waiting);
        command.AddInteger("$called", (int)TurnState.Called);
        command.AddInteger("$in_service", (int)TurnState.InService);

        using var reader = command.ExecuteReader();
        var turns = new List<Turn>();
        while (reader.Read())
        {
            turns.Add(ReadTurn(reader));
        }

        return turns;
    }

    public IReadOnlyList<Turn> ListAssignedToBarber(Guid barberId)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, ticket_number, display_ticket_number, ticket_date, state, source, checked_in_at, assigned_barber_id,
                   appointment_id, requested_barber_ids, customer_name
            FROM turns
            WHERE assigned_barber_id = $barber_id
              AND state = $called
            ORDER BY checked_in_at, ticket_number;
            """;
        command.AddText("$barber_id", barberId.ToString());
        command.AddInteger("$called", (int)TurnState.Called);

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

    public void MarkCancelled(Guid turnId, DateTimeOffset updatedAt)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            UPDATE turns
            SET state = $state,
                updated_at = $updated_at
            WHERE id = $turn_id
              AND state IN ($waiting, $called, $in_service);
            """;
        command.AddText("$turn_id", turnId.ToString());
        command.AddInteger("$state", (int)TurnState.Cancelled);
        command.AddInteger("$waiting", (int)TurnState.Waiting);
        command.AddInteger("$called", (int)TurnState.Called);
        command.AddInteger("$in_service", (int)TurnState.InService);
        command.AddText("$updated_at", updatedAt.ToString("O"));

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("Active turn was not found for cancellation.");
        }
    }

    public void MarkInService(Guid turnId, Guid barberId, DateTimeOffset updatedAt)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            UPDATE turns
            SET state = $state,
                updated_at = $updated_at
            WHERE id = $turn_id
              AND assigned_barber_id = $barber_id
              AND state = $called;
            """;
        command.AddText("$turn_id", turnId.ToString());
        command.AddText("$barber_id", barberId.ToString());
        command.AddInteger("$state", (int)TurnState.InService);
        command.AddInteger("$called", (int)TurnState.Called);
        command.AddText("$updated_at", updatedAt.ToString("O"));

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("Called turn was not found for service start.");
        }
    }

    private static Turn ReadTurn(SqliteDataReader reader)
    {
        return new Turn(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetInt32(2),
            DateOnly.Parse(reader.GetString(3)),
            (TurnState)reader.GetInt32(4),
            (TurnSource)reader.GetInt32(5),
            DateTimeOffset.Parse(reader.GetString(6)),
            reader.IsDBNull(7) ? null : Guid.Parse(reader.GetString(7)),
            reader.IsDBNull(8) ? null : Guid.Parse(reader.GetString(8)),
            ParseIds(reader.IsDBNull(9) ? null : reader.GetString(9)),
            reader.IsDBNull(10) ? null : reader.GetString(10));
    }

    private static string FormatDate(DateOnly value)
    {
        return value.ToString("yyyy-MM-dd");
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
