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
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS appointment_reservations (
                id TEXT NOT NULL PRIMARY KEY,
                barber_id TEXT NOT NULL,
                state INTEGER NOT NULL,
                scheduled_for TEXT NOT NULL,
                protection_window_minutes INTEGER NOT NULL,
                updated_at TEXT NOT NULL,
                FOREIGN KEY (barber_id) REFERENCES barbers(id)
            );

            CREATE TABLE IF NOT EXISTS turns (
                id TEXT NOT NULL PRIMARY KEY,
                ticket_number TEXT NOT NULL UNIQUE,
                state INTEGER NOT NULL,
                source INTEGER NOT NULL,
                customer_name TEXT NULL,
                checked_in_at TEXT NOT NULL,
                assigned_barber_id TEXT NULL,
                appointment_id TEXT NULL,
                requested_barber_ids TEXT NULL,
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
                amount_cents INTEGER NOT NULL,
                currency TEXT NOT NULL,
                collected_at TEXT NOT NULL,
                device_id TEXT NOT NULL,
                receipt_number TEXT NULL,
                cash_drawer_opened INTEGER NOT NULL,
                commission_cents INTEGER NULL,
                FOREIGN KEY (turn_id) REFERENCES turns(id),
                FOREIGN KEY (barber_id) REFERENCES barbers(id)
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
            """;
        command.ExecuteNonQuery();

        EnsureColumn(connection, "turns", "customer_name", "TEXT NULL");
        EnsureColumn(connection, "barbers", "profile_image_path", "TEXT NULL");
        EnsureColumn(connection, "barbers", "is_active", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumn(connection, "barbers", "station_number", "INTEGER NULL");
        NormalizeBarberStations(connection);
        EnsureActiveStationIndex(connection);
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
                ORDER BY rotation_order, display_name, id;
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
}
