using Barberia.Data.Models;
using Microsoft.Data.Sqlite;

namespace Barberia.Data.Repositories;

public sealed class CashBoxDailyOpeningRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;

    public CashBoxDailyOpeningRepository(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public CashBoxDailyOpening? GetByBusinessDate(DateOnly businessDate)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT business_date, opening_balance_cents, currency, opened_at, opened_device_id, updated_at, updated_device_id
            FROM cash_box_daily_openings
            WHERE business_date = $business_date;
            """;
        command.AddText("$business_date", FormatDate(businessDate));

        using var reader = command.ExecuteReader();
        return reader.Read()
            ? ReadOpening(reader)
            : null;
    }

    public void Save(CashBoxDailyOpening opening)
    {
        ArgumentNullException.ThrowIfNull(opening);

        if (opening.OpeningBalanceCents < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(opening), "Opening balance cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(opening.Currency)
            || string.IsNullOrWhiteSpace(opening.OpenedDeviceId)
            || string.IsNullOrWhiteSpace(opening.UpdatedDeviceId))
        {
            throw new ArgumentException("Currency and device ids are required.", nameof(opening));
        }

        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            INSERT INTO cash_box_daily_openings (
                business_date, opening_balance_cents, currency, opened_at, opened_device_id, updated_at, updated_device_id
            ) VALUES (
                $business_date, $opening_balance_cents, $currency, $opened_at, $opened_device_id, $updated_at, $updated_device_id
            )
            ON CONFLICT(business_date) DO UPDATE SET
                opening_balance_cents = excluded.opening_balance_cents,
                currency = excluded.currency,
                updated_at = excluded.updated_at,
                updated_device_id = excluded.updated_device_id;
            """;
        command.AddText("$business_date", FormatDate(opening.BusinessDate));
        command.AddInteger("$opening_balance_cents", opening.OpeningBalanceCents);
        command.AddText("$currency", opening.Currency);
        command.AddText("$opened_at", opening.OpenedAt.ToString("O"));
        command.AddText("$opened_device_id", opening.OpenedDeviceId);
        command.AddText("$updated_at", opening.UpdatedAt.ToString("O"));
        command.AddText("$updated_device_id", opening.UpdatedDeviceId);
        command.ExecuteNonQuery();
    }

    private static CashBoxDailyOpening ReadOpening(SqliteDataReader reader)
    {
        return new CashBoxDailyOpening(
            DateOnly.Parse(reader.GetString(0)),
            reader.GetInt64(1),
            reader.GetString(2),
            DateTimeOffset.Parse(reader.GetString(3)),
            reader.GetString(4),
            DateTimeOffset.Parse(reader.GetString(5)),
            reader.GetString(6));
    }

    private static string FormatDate(DateOnly value)
    {
        return value.ToString("yyyy-MM-dd");
    }
}
