using Microsoft.Data.Sqlite;

namespace Barberia.Data;

internal static class SqliteCommandExtensions
{
    internal static void AddText(this SqliteCommand command, string name, string? value)
    {
        command.Parameters.AddWithValue(name, value is null ? DBNull.Value : value);
    }

    internal static void AddInteger(this SqliteCommand command, string name, int value)
    {
        command.Parameters.AddWithValue(name, value);
    }

    internal static void AddInteger(this SqliteCommand command, string name, long value)
    {
        command.Parameters.AddWithValue(name, value);
    }
}
