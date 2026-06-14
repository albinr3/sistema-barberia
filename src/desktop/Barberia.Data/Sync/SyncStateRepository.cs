using Microsoft.Data.Sqlite;

namespace Barberia.Data.Sync;

public sealed class SyncStateRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;

    public SyncStateRepository(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public string? GetValue(string key)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT value
            FROM sync_state
            WHERE key = $key;
            """;
        command.Parameters.AddWithValue("$key", key);

        var value = command.ExecuteScalar();
        return value is null || value == DBNull.Value ? null : Convert.ToString(value);
    }

    public void SetValue(string key, string? value, DateTimeOffset updatedAt)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            INSERT INTO sync_state (key, value, updated_at)
            VALUES ($key, $value, $updated_at)
            ON CONFLICT(key) DO UPDATE SET
                value = excluded.value,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value is null ? DBNull.Value : value);
        command.Parameters.AddWithValue("$updated_at", updatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }
}
