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
                checked_in_at TEXT NULL,
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
            """;
        command.ExecuteNonQuery();
    }
}
