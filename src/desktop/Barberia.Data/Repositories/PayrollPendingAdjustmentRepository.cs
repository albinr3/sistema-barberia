using Barberia.Data.Models;
using Microsoft.Data.Sqlite;

namespace Barberia.Data.Repositories;

public sealed class PayrollPendingAdjustmentRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;

    public PayrollPendingAdjustmentRepository(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public IReadOnlyList<PayrollPendingAdjustment> ListByRange(DateTimeOffset startDate, DateTimeOffset endDate)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, command_id, start_date, end_date, barber_id, amount_cents, reason, created_at
            FROM payroll_pending_adjustments
            WHERE start_date = $start_date AND end_date = $end_date
            ORDER BY created_at, id;
            """;
        command.AddText("$start_date", startDate.ToString("O"));
        command.AddText("$end_date", endDate.ToString("O"));

        var list = new List<PayrollPendingAdjustment>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadPendingAdjustment(reader));
        }

        return list;
    }

    public void Add(PayrollPendingAdjustment adjustment)
    {
        if (adjustment.Id == Guid.Empty || adjustment.CommandId == Guid.Empty || adjustment.BarberId == Guid.Empty)
        {
            throw new ArgumentException("Adjustment, command and barber ids are required.", nameof(adjustment));
        }

        if (string.IsNullOrWhiteSpace(adjustment.Reason))
        {
            throw new ArgumentException("Adjustment reason is required.", nameof(adjustment));
        }

        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            INSERT INTO payroll_pending_adjustments (
                id, command_id, start_date, end_date, barber_id, amount_cents, reason, created_at
            ) VALUES (
                $id, $command_id, $start_date, $end_date, $barber_id, $amount_cents, $reason, $created_at
            );
            """;
        command.AddText("$id", adjustment.Id.ToString());
        command.AddText("$command_id", adjustment.CommandId.ToString());
        command.AddText("$start_date", adjustment.StartDate.ToString("O"));
        command.AddText("$end_date", adjustment.EndDate.ToString("O"));
        command.AddText("$barber_id", adjustment.BarberId.ToString());
        command.AddInteger("$amount_cents", adjustment.AmountCents);
        command.AddText("$reason", adjustment.Reason.Trim());
        command.AddText("$created_at", adjustment.CreatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void ClearRange(DateTimeOffset startDate, DateTimeOffset endDate)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            DELETE FROM payroll_pending_adjustments
            WHERE start_date = $start_date AND end_date = $end_date;
            """;
        command.AddText("$start_date", startDate.ToString("O"));
        command.AddText("$end_date", endDate.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static PayrollPendingAdjustment ReadPendingAdjustment(SqliteDataReader reader)
    {
        return new PayrollPendingAdjustment(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            DateTimeOffset.Parse(reader.GetString(2)),
            DateTimeOffset.Parse(reader.GetString(3)),
            Guid.Parse(reader.GetString(4)),
            reader.GetInt64(5),
            reader.GetString(6),
            DateTimeOffset.Parse(reader.GetString(7)));
    }
}
