using Barberia.Core.Domain;
using Microsoft.Data.Sqlite;

namespace Barberia.Data.Reports;

public sealed class LocalAdminReportRepository
{
    private const string DefaultCurrency = "USD";
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;

    public LocalAdminReportRepository(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public LocalAdminReportSnapshot Load(DateTimeOffset fromInclusive, DateTimeOffset toExclusive, DateTimeOffset generatedAt)
    {
        if (fromInclusive >= toExclusive)
        {
            throw new ArgumentException("Report start must be before report end.", nameof(fromInclusive));
        }

        return new LocalAdminReportSnapshot(
            fromInclusive,
            toExclusive,
            generatedAt,
            LoadOperations(fromInclusive, toExclusive),
            LoadCash(fromInclusive, toExclusive),
            LoadBarbers(fromInclusive, toExclusive),
            LoadRecentPayments(fromInclusive, toExclusive));
    }

    private OperationReportSummary LoadOperations(DateTimeOffset fromInclusive, DateTimeOffset toExclusive)
    {
        using var command = CreateCommand();
        command.CommandText = """
            SELECT
                COUNT(*) AS check_ins,
                COALESCE(SUM(CASE WHEN source = $walk_in THEN 1 ELSE 0 END), 0) AS walk_ins,
                COALESCE(SUM(CASE WHEN source = $appointment THEN 1 ELSE 0 END), 0) AS appointments,
                COALESCE(SUM(CASE WHEN state IN ($waiting, $called, $in_service) THEN 1 ELSE 0 END), 0) AS active_turns,
                COALESCE(SUM(CASE WHEN state = $no_show THEN 1 ELSE 0 END), 0) AS no_shows,
                COALESCE(SUM(CASE WHEN state = $cancelled THEN 1 ELSE 0 END), 0) AS cancelled
            FROM turns
            WHERE checked_in_at >= $from
              AND checked_in_at < $to;
            """;
        AddRange(command, fromInclusive, toExclusive);
        command.AddInteger("$walk_in", (int)TurnSource.WalkIn);
        command.AddInteger("$appointment", (int)TurnSource.Appointment);
        command.AddInteger("$waiting", (int)TurnState.Waiting);
        command.AddInteger("$called", (int)TurnState.Called);
        command.AddInteger("$in_service", (int)TurnState.InService);
        command.AddInteger("$no_show", (int)TurnState.NoShow);
        command.AddInteger("$cancelled", (int)TurnState.Cancelled);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return new OperationReportSummary(0, 0, 0, 0, 0, 0, 0);
        }

        return new OperationReportSummary(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            LoadCompletedServiceCount(fromInclusive, toExclusive),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5));
    }

    private int LoadCompletedServiceCount(DateTimeOffset fromInclusive, DateTimeOffset toExclusive)
    {
        using var command = CreateCommand();
        command.CommandText = """
            SELECT COUNT(DISTINCT turn_id)
            FROM cash_payments
            WHERE collected_at >= $from
              AND collected_at < $to;
            """;
        AddRange(command, fromInclusive, toExclusive);

        return Convert.ToInt32(command.ExecuteScalar());
    }

    private CashReportSummary LoadCash(DateTimeOffset fromInclusive, DateTimeOffset toExclusive)
    {
        using var command = CreateCommand();
        command.CommandText = """
            SELECT
                COUNT(*) AS total_payment_count,
                COALESCE(SUM(amount_cents), 0) AS total_amount_cents,
                COALESCE(SUM(CASE WHEN payment_method = 0 THEN amount_cents ELSE 0 END), 0) AS cash_amount_cents,
                COALESCE(SUM(CASE WHEN payment_method = 1 THEN amount_cents ELSE 0 END), 0) AS zelle_amount_cents,
                COALESCE(SUM(CASE WHEN payment_method = 0 THEN 1 ELSE 0 END), 0) AS cash_payment_count,
                COALESCE(SUM(CASE WHEN payment_method = 1 THEN 1 ELSE 0 END), 0) AS zelle_payment_count,
                COALESCE(SUM(CASE WHEN commission_cents IS NULL THEN 0 ELSE commission_cents END), 0) AS commission_cents,
                COALESCE(SUM(CASE WHEN commission_cents IS NULL THEN 1 ELSE 0 END), 0) AS payments_missing_commission,
                COALESCE(SUM(CASE WHEN cash_drawer_opened = 1 THEN 1 ELSE 0 END), 0) AS drawer_open_count,
                COALESCE(MIN(currency), $currency) AS currency
            FROM cash_payments
            WHERE collected_at >= $from
              AND collected_at < $to;
            """;
        AddRange(command, fromInclusive, toExclusive);
        command.AddText("$currency", DefaultCurrency);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return new CashReportSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, DefaultCurrency);
        }

        return new CashReportSummary(
            reader.GetInt32(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetInt64(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetInt64(6),
            reader.GetInt32(7),
            reader.GetInt32(8),
            reader.GetString(9));
    }

    private IReadOnlyList<BarberReportRow> LoadBarbers(DateTimeOffset fromInclusive, DateTimeOffset toExclusive)
    {
        using var command = CreateCommand();
        command.CommandText = """
            SELECT
                b.id,
                b.display_name,
                b.station_number,
                COUNT(p.id) AS services_closed,
                COALESCE(SUM(p.amount_cents), 0) AS cash_collected_cents,
                COALESCE(SUM(CASE WHEN p.commission_cents IS NULL THEN 0 ELSE p.commission_cents END), 0) AS commission_cents,
                COALESCE(SUM(CASE WHEN p.id IS NOT NULL AND p.commission_cents IS NULL THEN 1 ELSE 0 END), 0) AS missing_commission,
                COALESCE(SUM(CASE WHEN p.cash_drawer_opened = 1 THEN 1 ELSE 0 END), 0) AS drawer_opens
            FROM barbers b
            LEFT JOIN cash_payments p
                ON p.barber_id = b.id
               AND p.collected_at >= $from
               AND p.collected_at < $to
            GROUP BY b.id, b.display_name, b.station_number
            ORDER BY CASE WHEN b.station_number IS NULL THEN 1 ELSE 0 END, b.station_number ASC, b.display_name ASC;
            """;
        AddRange(command, fromInclusive, toExclusive);

        using var reader = command.ExecuteReader();
        var rows = new List<BarberReportRow>();
        while (reader.Read())
        {
            rows.Add(new BarberReportRow(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt64(4),
                reader.GetInt64(5),
                reader.GetInt32(6),
                reader.GetInt32(7)));
        }

        return rows;
    }

    private IReadOnlyList<CashPaymentReportRow> LoadRecentPayments(DateTimeOffset fromInclusive, DateTimeOffset toExclusive)
    {
        using var command = CreateCommand();
        command.CommandText = """
            SELECT
                p.id,
                p.turn_id,
                COALESCE(t.display_ticket_number, 0) AS display_ticket_number,
                COALESCE(t.ticket_number, 'No ticket') AS internal_ticket_number,
                p.barber_id,
                COALESCE(b.display_name, 'Local barber') AS barber_name,
                b.station_number,
                s.name AS service_name,
                p.service_price_cents,
                p.additional_cents,
                p.amount_cents,
                p.currency,
                p.collected_at,
                p.device_id,
                p.receipt_number,
                p.cash_drawer_opened,
                p.commission_cents,
                p.payment_method,
                p.payment_reference
            FROM cash_payments p
            LEFT JOIN turns t ON t.id = p.turn_id
            LEFT JOIN barbers b ON b.id = p.barber_id
            LEFT JOIN services s ON s.id = p.service_id
            WHERE p.collected_at >= $from
              AND p.collected_at < $to
            ORDER BY p.collected_at DESC, p.receipt_number DESC
            LIMIT 20;
            """;
        AddRange(command, fromInclusive, toExclusive);

        using var reader = command.ExecuteReader();
        var rows = new List<CashPaymentReportRow>();
        while (reader.Read())
        {
            rows.Add(new CashPaymentReportRow(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.GetInt32(2),
                reader.GetString(3),
                Guid.Parse(reader.GetString(4)),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetInt64(8),
                reader.GetInt64(9),
                reader.GetInt64(10),
                reader.GetString(11),
                DateTimeOffset.Parse(reader.GetString(12)),
                reader.GetString(13),
                reader.IsDBNull(14) ? null : reader.GetString(14),
                reader.GetInt32(15) == 1,
                reader.IsDBNull(16) ? null : reader.GetInt64(16),
                (Barberia.Data.Models.CustomerPaymentMethod)reader.GetInt32(17),
                reader.IsDBNull(18) ? null : reader.GetString(18)));
        }

        return rows;
    }

    private SqliteCommand CreateCommand()
    {
        var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        return command;
    }

    private static void AddRange(SqliteCommand command, DateTimeOffset fromInclusive, DateTimeOffset toExclusive)
    {
        command.AddText("$from", fromInclusive.ToString("O"));
        command.AddText("$to", toExclusive.ToString("O"));
    }
}
