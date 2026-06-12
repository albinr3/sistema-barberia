using Barberia.Data.Models;
using Microsoft.Data.Sqlite;

namespace Barberia.Data.Repositories;

public sealed class DailyRotationRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;

    public DailyRotationRepository(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public IReadOnlyList<DailyRotationEntry> ListByDate(DateOnly businessDate)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT business_date, barber_id, queue_position, arrived_at, updated_at
            FROM barber_daily_rotation
            WHERE business_date = $business_date
            ORDER BY queue_position, arrived_at, barber_id;
            """;
        command.AddText("$business_date", FormatDate(businessDate));

        using var reader = command.ExecuteReader();
        var entries = new List<DailyRotationEntry>();
        while (reader.Read())
        {
            entries.Add(ReadEntry(reader));
        }

        return entries;
    }

    public DailyRotationEntry? Get(DateOnly businessDate, Guid barberId)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT business_date, barber_id, queue_position, arrived_at, updated_at
            FROM barber_daily_rotation
            WHERE business_date = $business_date
              AND barber_id = $barber_id;
            """;
        command.AddText("$business_date", FormatDate(businessDate));
        command.AddText("$barber_id", barberId.ToString());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadEntry(reader) : null;
    }

    public void EnsureQueued(DateOnly businessDate, Guid barberId, DateTimeOffset arrivedAt, DateTimeOffset updatedAt)
    {
        if (Get(businessDate, barberId) is not null)
        {
            return;
        }

        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            INSERT INTO barber_daily_rotation (
                business_date, barber_id, queue_position, arrived_at, updated_at
            ) VALUES (
                $business_date, $barber_id,
                (
                    SELECT COALESCE(MAX(queue_position), -1) + 1
                    FROM barber_daily_rotation
                    WHERE business_date = $business_date
                ),
                $arrived_at, $updated_at
            );
            """;
        command.AddText("$business_date", FormatDate(businessDate));
        command.AddText("$barber_id", barberId.ToString());
        command.AddText("$arrived_at", arrivedAt.ToString("O"));
        command.AddText("$updated_at", updatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void MoveToEnd(DateOnly businessDate, Guid barberId, DateTimeOffset arrivedAt, DateTimeOffset updatedAt)
    {
        var existingEntries = ListByDate(businessDate)
            .Where(entry => entry.BarberId != barberId)
            .ToList();
        var existingEntry = Get(businessDate, barberId);
        var movedEntry = new DailyRotationEntry(
            businessDate,
            barberId,
            existingEntries.Count,
            existingEntry?.ArrivedAt ?? arrivedAt,
            updatedAt);

        existingEntries.Add(movedEntry);
        ReplaceDateQueue(businessDate, existingEntries, updatedAt);
    }

    public void DeleteForDate(DateOnly businessDate)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            DELETE FROM barber_daily_rotation
            WHERE business_date = $business_date;
            """;
        command.AddText("$business_date", FormatDate(businessDate));
        command.ExecuteNonQuery();
    }

    private void ReplaceDateQueue(DateOnly businessDate, IReadOnlyList<DailyRotationEntry> entries, DateTimeOffset updatedAt)
    {
        DeleteForDate(businessDate);

        for (var index = 0; index < entries.Count; index++)
        {
            using var command = _connection.CreateCommand();
            command.Transaction = _transaction;
            command.CommandText = """
                INSERT INTO barber_daily_rotation (
                    business_date, barber_id, queue_position, arrived_at, updated_at
                ) VALUES (
                    $business_date, $barber_id, $queue_position, $arrived_at, $updated_at
                );
                """;
            command.AddText("$business_date", FormatDate(businessDate));
            command.AddText("$barber_id", entries[index].BarberId.ToString());
            command.AddInteger("$queue_position", index);
            command.AddText("$arrived_at", entries[index].ArrivedAt.ToString("O"));
            command.AddText("$updated_at", updatedAt.ToString("O"));
            command.ExecuteNonQuery();
        }
    }

    private static DailyRotationEntry ReadEntry(SqliteDataReader reader)
    {
        return new DailyRotationEntry(
            DateOnly.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetInt32(2),
            DateTimeOffset.Parse(reader.GetString(3)),
            DateTimeOffset.Parse(reader.GetString(4)));
    }

    private static string FormatDate(DateOnly value)
    {
        return value.ToString("yyyy-MM-dd");
    }
}
