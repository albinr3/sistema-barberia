using Barberia.Core.Domain;
using Barberia.Data.Models;
using Microsoft.Data.Sqlite;

namespace Barberia.Data.Repositories;

public sealed class LocalTicketHistoryRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;

    public LocalTicketHistoryRepository(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public IReadOnlyList<TicketHistoryRow> ListRecentHistoryToday(int limit)
    {
        var now = DateTimeOffset.Now;
        var startOfDay = new DateTimeOffset(now.Date, now.Offset).ToString("yyyy-MM-dd");

        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT 
                t.ticket_number,
                t.display_ticket_number,
                t.customer_name,
                t.source,
                t.state,
                b.display_name,
                t.checked_in_at,
                t.started_at,
                p.collected_at,
                t.completed_at,
                t.cancelled_at,
                s.name,
                p.amount_cents,
                p.receipt_number,
                b.profile_image_path,
                p.payment_method,
                p.payment_reference
            FROM turns t
            LEFT JOIN barbers b ON t.assigned_barber_id = b.id
            LEFT JOIN cash_payments p ON t.id = p.turn_id
            LEFT JOIN services s ON REPLACE(LOWER(p.service_id), '-', '') = REPLACE(LOWER(s.id), '-', '')
            WHERE t.ticket_date = $ticket_date
              AND t.state IN ($completed, $cancelled, $noshow, $voided)
            ORDER BY COALESCE(t.updated_at, t.checked_in_at) DESC
            LIMIT $limit;
            """;
        
        command.AddText("$ticket_date", startOfDay);
        command.AddInteger("$completed", (int)TurnState.Completed);
        command.AddInteger("$cancelled", (int)TurnState.Cancelled);
        command.AddInteger("$noshow", (int)TurnState.NoShow);
        command.AddInteger("$voided", (int)TurnState.Voided);
        command.AddInteger("$limit", limit);

        return ExecuteQuery(command);
    }
    public int CountHistory(DateTimeOffset from, DateTimeOffset to, TurnState? state = null, Guid? barberId = null, string? searchQuery = null)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        var sql = """
            SELECT COUNT(*)
            FROM turns t
            LEFT JOIN barbers b ON t.assigned_barber_id = b.id
            LEFT JOIN cash_payments p ON t.id = p.turn_id
            WHERE t.checked_in_at >= $from AND t.checked_in_at < $to
            """;

        if (state.HasValue)
        {
            sql += "\n  AND t.state = $state";
            command.AddInteger("$state", (int)state.Value);
        }
        else
        {
            sql += "\n  AND t.state IN ($completed, $cancelled, $noshow, $voided)";
            command.AddInteger("$completed", (int)TurnState.Completed);
            command.AddInteger("$cancelled", (int)TurnState.Cancelled);
            command.AddInteger("$noshow", (int)TurnState.NoShow);
            command.AddInteger("$voided", (int)TurnState.Voided);
        }

        if (barberId.HasValue)
        {
            sql += "\n  AND t.assigned_barber_id = $barber_id";
            command.AddText("$barber_id", barberId.Value.ToString());
        }

        sql += BuildSearchClause(command, searchQuery);

        command.CommandText = sql;
        
        command.AddText("$from", from.ToString("O"));
        command.AddText("$to", to.ToString("O"));

        var result = command.ExecuteScalar();
        return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
    }


    public IReadOnlyList<TicketHistoryRow> ListHistory(DateTimeOffset from, DateTimeOffset to, TurnState? state = null, Guid? barberId = null, string? searchQuery = null, int limit = 20, int offset = 0)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        var sql = """
            SELECT 
                t.ticket_number,
                t.display_ticket_number,
                t.customer_name,
                t.source,
                t.state,
                b.display_name,
                t.checked_in_at,
                t.started_at,
                p.collected_at,
                t.completed_at,
                t.cancelled_at,
                s.name,
                p.amount_cents,
                p.receipt_number,
                b.profile_image_path,
                p.payment_method,
                p.payment_reference
            FROM turns t
            LEFT JOIN barbers b ON t.assigned_barber_id = b.id
            LEFT JOIN cash_payments p ON t.id = p.turn_id
            LEFT JOIN services s ON REPLACE(LOWER(p.service_id), '-', '') = REPLACE(LOWER(s.id), '-', '')
            WHERE t.checked_in_at >= $from AND t.checked_in_at < $to
            """;

        if (state.HasValue)
        {
            sql += "\n  AND t.state = $state";
            command.AddInteger("$state", (int)state.Value);
        }
        else
        {
            sql += "\n  AND t.state IN ($completed, $cancelled, $noshow, $voided)";
            command.AddInteger("$completed", (int)TurnState.Completed);
            command.AddInteger("$cancelled", (int)TurnState.Cancelled);
            command.AddInteger("$noshow", (int)TurnState.NoShow);
            command.AddInteger("$voided", (int)TurnState.Voided);
        }

        if (barberId.HasValue)
        {
            sql += "\n  AND t.assigned_barber_id = $barber_id";
            command.AddText("$barber_id", barberId.Value.ToString());
        }

        sql += BuildSearchClause(command, searchQuery);

        sql += "\nORDER BY t.checked_in_at DESC";
        sql += "\nLIMIT $limit OFFSET $offset;";

        command.CommandText = sql;
        
        command.AddText("$from", from.ToString("O"));
        command.AddText("$to", to.ToString("O"));
        command.AddInteger("$limit", limit);
        command.AddInteger("$offset", offset);

        return ExecuteQuery(command);
    }

    private static IReadOnlyList<TicketHistoryRow> ExecuteQuery(SqliteCommand command)
    {
        var rows = new List<TicketHistoryRow>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var amountCents = reader.IsDBNull(12) ? (int?)null : reader.GetInt32(12);
            decimal? amount = amountCents.HasValue ? amountCents.Value / 100m : null;
            
            CustomerPaymentMethod? paymentMethod = null;
            if (!reader.IsDBNull(15))
            {
                paymentMethod = (CustomerPaymentMethod)reader.GetInt32(15);
            }
            
            string? paymentReference = reader.IsDBNull(16) ? null : reader.GetString(16);

            string? paymentResultText = null;
            if (amount.HasValue)
            {
                if (paymentMethod == CustomerPaymentMethod.Zelle)
                {
                    paymentResultText = $"Paid {amount.Value:C} by Zelle" + (string.IsNullOrWhiteSpace(paymentReference) ? "" : $" (Ref: {paymentReference})");
                }
                else
                {
                    paymentResultText = $"Paid {amount.Value:C} in cash";
                }
            }
            
            rows.Add(new TicketHistoryRow(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                (TurnSource)reader.GetInt32(3),
                (TurnState)reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(14) ? null : reader.GetString(14),
                DateTimeOffset.Parse(reader.GetString(6)),
                reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)),
                reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8)),
                reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9)),
                reader.IsDBNull(10) ? null : DateTimeOffset.Parse(reader.GetString(10)),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                amount,
                reader.IsDBNull(13) ? null : reader.GetString(13),
                paymentMethod,
                paymentReference,
                paymentResultText
            ));
        }

        return rows;
    }

    private static string BuildSearchClause(SqliteCommand command, string? searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return string.Empty;
        }

        var normalized = searchQuery.Trim();
        command.AddText("$search", $"%{normalized}%");

        if (int.TryParse(normalized.TrimStart('#'), out var displayTicketNumber) && displayTicketNumber > 0)
        {
            command.AddInteger("$display_ticket_number_search", displayTicketNumber);
            return "\n  AND (t.customer_name LIKE $search OR t.display_ticket_number = $display_ticket_number_search)";
        }

        if (normalized.StartsWith("W", StringComparison.OrdinalIgnoreCase))
        {
            command.AddText("$ticket_number_search", normalized);
            return "\n  AND (t.customer_name LIKE $search OR t.ticket_number = $ticket_number_search)";
        }

        return "\n  AND t.customer_name LIKE $search";
    }
}
