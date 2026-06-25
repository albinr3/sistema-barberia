using Microsoft.Data.Sqlite;

namespace Barberia.Data.Repositories;

internal static class SqliteForeignKeyIds
{
    public static string ExistingId(SqliteConnection connection, SqliteTransaction? transaction, string tableName, Guid id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT id
            FROM {tableName}
            WHERE id = $id OR id = $id_n
            LIMIT 1;
            """;
        command.AddText("$id", id.ToString());
        command.AddText("$id_n", id.ToString("N"));

        return command.ExecuteScalar() as string ?? id.ToString();
    }
}
