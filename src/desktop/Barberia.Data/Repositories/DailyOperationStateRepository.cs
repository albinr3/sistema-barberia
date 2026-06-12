using Microsoft.Data.Sqlite;

namespace Barberia.Data.Repositories;

public sealed class DailyOperationStateRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;

    public DailyOperationStateRepository(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public DateOnly? GetLastResetDate()
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT business_date
            FROM daily_operation_state
            ORDER BY business_date DESC
            LIMIT 1;
            """;

        var value = command.ExecuteScalar();
        return value is null || value == DBNull.Value
            ? null
            : DateOnly.Parse((string)value);
    }

    public bool HasResetFor(DateOnly businessDate)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT COUNT(*)
            FROM daily_operation_state
            WHERE business_date = $business_date;
            """;
        command.AddText("$business_date", FormatDate(businessDate));

        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    public void MarkResetApplied(DateOnly businessDate, DateTimeOffset appliedAt)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            INSERT INTO daily_operation_state (
                business_date, reset_applied_at, updated_at
            ) VALUES (
                $business_date, $reset_applied_at, $updated_at
            )
            ON CONFLICT(business_date) DO UPDATE SET
                reset_applied_at = excluded.reset_applied_at,
                updated_at = excluded.updated_at;
            """;
        command.AddText("$business_date", FormatDate(businessDate));
        command.AddText("$reset_applied_at", appliedAt.ToString("O"));
        command.AddText("$updated_at", appliedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static string FormatDate(DateOnly value)
    {
        return value.ToString("yyyy-MM-dd");
    }
}
