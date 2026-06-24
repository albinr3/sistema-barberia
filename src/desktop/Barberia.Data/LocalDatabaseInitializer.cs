using Microsoft.Data.Sqlite;

namespace Barberia.Data;

public sealed class LocalDatabaseInitializer
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public LocalDatabaseInitializer(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Initialize()
    {
        using var connection = _connectionFactory.OpenConnection();
        Initialize(connection);
    }

    public static void Initialize(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS barbers (
                id TEXT NOT NULL PRIMARY KEY,
                display_name TEXT NOT NULL,
                state INTEGER NOT NULL,
                clients_served_today INTEGER NOT NULL,
                rotation_order INTEGER NOT NULL,
                station_number INTEGER NULL,
                checked_in_at TEXT NULL,
                profile_image_path TEXT NULL,
                is_active INTEGER NOT NULL DEFAULT 1,
                commission_percentage INTEGER NOT NULL DEFAULT 65,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS daily_operation_state (
                business_date TEXT NOT NULL PRIMARY KEY,
                reset_applied_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS barber_daily_rotation (
                business_date TEXT NOT NULL,
                barber_id TEXT NOT NULL,
                queue_position INTEGER NOT NULL,
                arrived_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                PRIMARY KEY (business_date, barber_id),
                FOREIGN KEY (barber_id) REFERENCES barbers(id)
            );

            CREATE INDEX IF NOT EXISTS idx_barber_daily_rotation_order
                ON barber_daily_rotation(business_date, queue_position, arrived_at);

            CREATE TABLE IF NOT EXISTS appointment_reservations (
                id TEXT NOT NULL PRIMARY KEY,
                barber_id TEXT NOT NULL,
                service_id TEXT NULL,
                appointment_code TEXT NULL,
                customer_name TEXT NULL,
                state INTEGER NOT NULL,
                scheduled_for TEXT NOT NULL,
                ends_at TEXT NULL,
                protection_window_minutes INTEGER NOT NULL,
                checked_in_at TEXT NULL,
                no_show_at TEXT NULL,
                completed_at TEXT NULL,
                updated_at TEXT NOT NULL,
                FOREIGN KEY (barber_id) REFERENCES barbers(id)
            );

            CREATE TABLE IF NOT EXISTS turns (
                id TEXT NOT NULL PRIMARY KEY,
                ticket_number TEXT NOT NULL UNIQUE,
                display_ticket_number INTEGER NULL,
                ticket_date TEXT NULL,
                state INTEGER NOT NULL,
                source INTEGER NOT NULL,
                customer_name TEXT NULL,
                checked_in_at TEXT NOT NULL,
                assigned_barber_id TEXT NULL,
                appointment_id TEXT NULL,
                requested_barber_ids TEXT NULL,
                started_at TEXT NULL,
                completed_at TEXT NULL,
                cancelled_at TEXT NULL,
                updated_at TEXT NOT NULL,
                FOREIGN KEY (assigned_barber_id) REFERENCES barbers(id),
                FOREIGN KEY (appointment_id) REFERENCES appointment_reservations(id)
            );

            CREATE INDEX IF NOT EXISTS idx_turns_waiting_order
                ON turns(state, checked_in_at, ticket_number);

            CREATE TABLE IF NOT EXISTS cash_payments (
                id TEXT NOT NULL PRIMARY KEY,
                turn_id TEXT NOT NULL,
                barber_id TEXT NOT NULL,
                service_id TEXT NULL,
                amount_cents INTEGER NOT NULL,
                currency TEXT NOT NULL,
                collected_at TEXT NOT NULL,
                device_id TEXT NOT NULL,
                receipt_number TEXT NULL,
                cash_drawer_opened INTEGER NOT NULL,
                commission_cents INTEGER NULL,
                service_price_cents INTEGER NULL,
                additional_cents INTEGER NOT NULL DEFAULT 0,
                payment_method INTEGER NOT NULL DEFAULT 0,
                payment_reference TEXT NULL,
                FOREIGN KEY (turn_id) REFERENCES turns(id),
                FOREIGN KEY (barber_id) REFERENCES barbers(id),
                FOREIGN KEY (service_id) REFERENCES services(id)
            );

            CREATE TABLE IF NOT EXISTS pending_service_payments (
                id TEXT NOT NULL PRIMARY KEY,
                turn_id TEXT NOT NULL,
                barber_id TEXT NOT NULL,
                service_id TEXT NOT NULL,
                business_date TEXT NOT NULL,
                service_price_cents INTEGER NOT NULL,
                additional_cents INTEGER NOT NULL DEFAULT 0,
                amount_cents INTEGER NOT NULL,
                commission_cents INTEGER NOT NULL,
                currency TEXT NOT NULL,
                device_id TEXT NOT NULL,
                pending_at TEXT NOT NULL,
                paid_at TEXT NULL,
                voided_at TEXT NULL,
                receipt_number TEXT NULL,
                payment_method INTEGER NULL,
                payment_reference TEXT NULL,
                FOREIGN KEY (turn_id) REFERENCES turns(id),
                FOREIGN KEY (barber_id) REFERENCES barbers(id),
                FOREIGN KEY (service_id) REFERENCES services(id)
            );

            CREATE INDEX IF NOT EXISTS idx_pending_service_payments_open_day
                ON pending_service_payments(business_date, pending_at)
                WHERE paid_at IS NULL AND voided_at IS NULL;

            CREATE UNIQUE INDEX IF NOT EXISTS idx_pending_service_payments_open_turn
                ON pending_service_payments(turn_id)
                WHERE paid_at IS NULL AND voided_at IS NULL;

            CREATE TABLE IF NOT EXISTS services (
                id TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                price_cents INTEGER NOT NULL,
                is_active INTEGER NOT NULL DEFAULT 1,
                display_order INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                CHECK (price_cents > 0)
            );

            CREATE TABLE IF NOT EXISTS audit_events (
                id TEXT NOT NULL PRIMARY KEY,
                occurred_at TEXT NOT NULL,
                event_type TEXT NOT NULL,
                aggregate_type TEXT NOT NULL,
                aggregate_id TEXT NOT NULL,
                device_id TEXT NULL,
                payload TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_audit_events_order
                ON audit_events(occurred_at, event_type);

            CREATE TABLE IF NOT EXISTS sync_outbox_events (
                id TEXT NOT NULL PRIMARY KEY,
                occurred_at TEXT NOT NULL,
                event_type TEXT NOT NULL,
                aggregate_type TEXT NOT NULL,
                aggregate_id TEXT NOT NULL,
                payload TEXT NOT NULL,
                device_id TEXT NULL,
                created_at TEXT NOT NULL,
                state INTEGER NOT NULL,
                attempt_count INTEGER NOT NULL,
                next_attempt_at TEXT NULL,
                last_attempted_at TEXT NULL,
                synced_at TEXT NULL,
                last_error TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_sync_outbox_pending
                ON sync_outbox_events(state, next_attempt_at, occurred_at);

            CREATE INDEX IF NOT EXISTS idx_sync_outbox_aggregate
                ON sync_outbox_events(aggregate_type, aggregate_id);

            CREATE TABLE IF NOT EXISTS sync_state (
                key TEXT NOT NULL PRIMARY KEY,
                value TEXT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS payroll_periods (
                id TEXT NOT NULL PRIMARY KEY,
                start_date TEXT NOT NULL,
                end_date TEXT NOT NULL,
                state INTEGER NOT NULL,
                total_services INTEGER NOT NULL,
                total_commission_cents INTEGER NOT NULL,
                total_adjustments_cents INTEGER NOT NULL,
                total_to_pay_cents INTEGER NOT NULL,
                payment_method INTEGER NULL,
                payment_reference TEXT NULL,
                notes TEXT NULL,
                generated_at TEXT NOT NULL,
                paid_at TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS payroll_lines (
                id TEXT NOT NULL PRIMARY KEY,
                period_id TEXT NOT NULL,
                barber_id TEXT NOT NULL,
                barber_name TEXT NOT NULL,
                station_number INTEGER NULL,
                closed_services_count INTEGER NOT NULL,
                cash_generated_cents INTEGER NOT NULL,
                commission_cents INTEGER NOT NULL,
                adjustments_cents INTEGER NOT NULL,
                total_cents INTEGER NOT NULL,
                FOREIGN KEY (period_id) REFERENCES payroll_periods(id),
                FOREIGN KEY (barber_id) REFERENCES barbers(id)
            );

            CREATE TABLE IF NOT EXISTS payroll_adjustments (
                id TEXT NOT NULL PRIMARY KEY,
                period_id TEXT NOT NULL,
                barber_id TEXT NOT NULL,
                amount_cents INTEGER NOT NULL,
                reason TEXT NOT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY (period_id) REFERENCES payroll_periods(id),
                FOREIGN KEY (barber_id) REFERENCES barbers(id)
            );

            CREATE TABLE IF NOT EXISTS payroll_payment_items (
                id TEXT NOT NULL PRIMARY KEY,
                period_id TEXT NOT NULL,
                barber_id TEXT NOT NULL,
                payment_id TEXT NOT NULL,
                FOREIGN KEY (period_id) REFERENCES payroll_periods(id),
                FOREIGN KEY (barber_id) REFERENCES barbers(id),
                FOREIGN KEY (payment_id) REFERENCES cash_payments(id)
            );

            CREATE TABLE IF NOT EXISTS payroll_pending_adjustments (
                id TEXT NOT NULL PRIMARY KEY,
                command_id TEXT NOT NULL UNIQUE,
                start_date TEXT NOT NULL,
                end_date TEXT NOT NULL,
                barber_id TEXT NOT NULL,
                amount_cents INTEGER NOT NULL,
                reason TEXT NOT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY (barber_id) REFERENCES barbers(id)
            );

            CREATE UNIQUE INDEX IF NOT EXISTS idx_payroll_periods_range
                ON payroll_periods(start_date, end_date);

            CREATE INDEX IF NOT EXISTS idx_payroll_lines_period
                ON payroll_lines(period_id);

            CREATE INDEX IF NOT EXISTS idx_payroll_adjustments_period
                ON payroll_adjustments(period_id);

            CREATE INDEX IF NOT EXISTS idx_payroll_payment_items_period
                ON payroll_payment_items(period_id);

            CREATE UNIQUE INDEX IF NOT EXISTS idx_payroll_payment_items_payment
                ON payroll_payment_items(payment_id);

            CREATE INDEX IF NOT EXISTS idx_payroll_pending_adjustments_range
                ON payroll_pending_adjustments(start_date, end_date);
            """;
        command.ExecuteNonQuery();

        EnsureColumn(connection, "turns", "customer_name", "TEXT NULL");
        EnsureColumn(connection, "turns", "display_ticket_number", "INTEGER NULL");
        EnsureColumn(connection, "turns", "ticket_date", "TEXT NULL");
        EnsureColumn(connection, "turns", "started_at", "TEXT NULL");
        EnsureColumn(connection, "turns", "completed_at", "TEXT NULL");
        EnsureColumn(connection, "turns", "cancelled_at", "TEXT NULL");
        EnsureColumn(connection, "appointment_reservations", "service_id", "TEXT NULL");
        EnsureColumn(connection, "appointment_reservations", "appointment_code", "TEXT NULL");
        EnsureColumn(connection, "appointment_reservations", "customer_name", "TEXT NULL");
        EnsureColumn(connection, "appointment_reservations", "ends_at", "TEXT NULL");
        EnsureColumn(connection, "appointment_reservations", "checked_in_at", "TEXT NULL");
        EnsureColumn(connection, "appointment_reservations", "no_show_at", "TEXT NULL");
        EnsureColumn(connection, "appointment_reservations", "completed_at", "TEXT NULL");
        BackfillDisplayTicketNumbers(connection);
        BackfillTurnLifecycleTimes(connection);
        EnsureDisplayTicketIndex(connection);
        EnsureTurnAppointmentIndex(connection);
        EnsureAppointmentCodeIndex(connection);
        EnsureColumn(connection, "barbers", "profile_image_path", "TEXT NULL");
        EnsureColumn(connection, "barbers", "is_active", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumn(connection, "barbers", "station_number", "INTEGER NULL");
        EnsureColumn(connection, "barbers", "commission_percentage", "INTEGER NOT NULL DEFAULT 65");
        BackfillBarberCommissionPercentage(connection);
        EnsureColumn(connection, "cash_payments", "service_id", "TEXT NULL");
        EnsureColumn(connection, "cash_payments", "service_price_cents", "INTEGER NULL");
        EnsureColumn(connection, "cash_payments", "additional_cents", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "cash_payments", "payment_method", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "cash_payments", "payment_reference", "TEXT NULL");
        NormalizeInactiveBarbers(connection);
        NormalizeBarberStations(connection);
        EnsureActiveStationIndex(connection);
    }

    private static void NormalizeInactiveBarbers(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE barbers
            SET station_number = NULL, state = 4
            WHERE is_active = 0;
            """;
        command.ExecuteNonQuery();
    }

    private static void BackfillDisplayTicketNumbers(SqliteConnection connection)
    {
        var turnsByDay = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT id, checked_in_at
                FROM turns
                WHERE display_ticket_number IS NULL
                   OR ticket_date IS NULL
                ORDER BY checked_in_at, ticket_number;
                """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetString(0);
                var checkedInAt = DateTimeOffset.Parse(reader.GetString(1));
                var ticketDate = DateOnly.FromDateTime(checkedInAt.DateTime).ToString("yyyy-MM-dd");
                if (!turnsByDay.TryGetValue(ticketDate, out var ids))
                {
                    ids = [];
                    turnsByDay.Add(ticketDate, ids);
                }

                ids.Add(id);
            }
        }

        foreach (var day in turnsByDay)
        {
            var nextNumber = GetMaxDisplayTicketNumber(connection, day.Key) + 1;
            foreach (var turnId in day.Value)
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    UPDATE turns
                    SET ticket_date = $ticket_date,
                        display_ticket_number = $display_ticket_number
                    WHERE id = $id;
                    """;
                command.AddText("$ticket_date", day.Key);
                command.AddInteger("$display_ticket_number", nextNumber);
                command.AddText("$id", turnId);
                command.ExecuteNonQuery();
                nextNumber++;
            }
        }
    }

    private static int GetMaxDisplayTicketNumber(SqliteConnection connection, string ticketDate)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(MAX(display_ticket_number), 0)
            FROM turns
            WHERE ticket_date = $ticket_date
              AND display_ticket_number IS NOT NULL;
            """;
        command.AddText("$ticket_date", ticketDate);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void EnsureDisplayTicketIndex(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE UNIQUE INDEX IF NOT EXISTS idx_turns_ticket_date_display_number
                ON turns(ticket_date, display_ticket_number)
                WHERE ticket_date IS NOT NULL AND display_ticket_number IS NOT NULL;
            """;
        command.ExecuteNonQuery();
    }

    private static void EnsureTurnAppointmentIndex(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE UNIQUE INDEX IF NOT EXISTS idx_turns_appointment_id_unique
                ON turns(appointment_id)
                WHERE appointment_id IS NOT NULL;
            """;
        command.ExecuteNonQuery();
    }

    private static void EnsureAppointmentCodeIndex(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE UNIQUE INDEX IF NOT EXISTS idx_appointment_reservations_code
                ON appointment_reservations(appointment_code)
                WHERE appointment_code IS NOT NULL;
            """;
        command.ExecuteNonQuery();
    }

    private static void NormalizeBarberStations(SqliteConnection connection)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                UPDATE barbers
                SET station_number = NULL
                WHERE is_active = 0
                  AND station_number IS NOT NULL;
                """;
            command.ExecuteNonQuery();
        }

        var activeBarbers = new List<(string Id, int? StationNumber)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT id, station_number
                FROM barbers
                WHERE is_active = 1
                ORDER BY COALESCE(station_number, 2147483647), display_name, id;
                """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                activeBarbers.Add((
                    reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetInt32(1)));
            }
        }

        var usedStations = new HashSet<int>();
        var updates = new List<(string Id, int StationNumber)>();
        var nextStation = 1;

        foreach (var barber in activeBarbers)
        {
            if (barber.StationNumber is int stationNumber
                && stationNumber > 0
                && usedStations.Add(stationNumber))
            {
                while (usedStations.Contains(nextStation))
                {
                    nextStation++;
                }

                continue;
            }

            while (usedStations.Contains(nextStation))
            {
                nextStation++;
            }

            usedStations.Add(nextStation);
            updates.Add((barber.Id, nextStation));
        }

        foreach (var update in updates)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE barbers
                SET station_number = $station_number
                WHERE id = $id;
                """;
            command.AddInteger("$station_number", update.StationNumber);
            command.AddText("$id", update.Id);
            command.ExecuteNonQuery();
        }
    }

    private static void EnsureActiveStationIndex(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE UNIQUE INDEX IF NOT EXISTS idx_barbers_active_station_number
                ON barbers(station_number)
                WHERE is_active = 1 AND station_number IS NOT NULL;
            """;
        command.ExecuteNonQuery();
    }

    private static void BackfillBarberCommissionPercentage(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE barbers
            SET commission_percentage = 65
            WHERE commission_percentage IS NULL;
            """;
        command.ExecuteNonQuery();
    }

    private static void EnsureColumn(SqliteConnection connection, string tableName, string columnName, string columnDefinition)
    {
        if (ColumnExists(connection, tableName, columnName))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        command.ExecuteNonQuery();
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void BackfillTurnLifecycleTimes(SqliteConnection connection)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                UPDATE turns
                SET completed_at = (
                    SELECT collected_at
                    FROM cash_payments
                    WHERE cash_payments.turn_id = turns.id
                    LIMIT 1
                )
                WHERE state = 4 AND completed_at IS NULL;
                """;
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                UPDATE turns
                SET completed_at = updated_at
                WHERE state = 4 AND completed_at IS NULL;
                """;
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                UPDATE turns
                SET cancelled_at = updated_at
                WHERE state IN (5, 6, 7) AND cancelled_at IS NULL;
                """;
            command.ExecuteNonQuery();
        }
    }
}
