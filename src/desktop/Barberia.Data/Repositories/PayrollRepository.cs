using Barberia.Data.Models;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Barberia.Data.Repositories;

public sealed class PayrollRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;

    public PayrollRepository(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public IReadOnlyList<PayrollPeriod> ListPeriods()
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, start_date, end_date, state, total_services, total_commission_cents, 
                   total_adjustments_cents, total_to_pay_cents, payment_method, payment_reference, 
                   notes, generated_at, paid_at
            FROM payroll_periods
            WHERE state = 1
            ORDER BY start_date DESC;
            """;

        var list = new List<PayrollPeriod>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadPeriod(reader));
        }
        return list;
    }

    public PayrollPeriod? GetPeriodByDates(DateTimeOffset startDate, DateTimeOffset endDate)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, start_date, end_date, state, total_services, total_commission_cents, 
                   total_adjustments_cents, total_to_pay_cents, payment_method, payment_reference, 
                   notes, generated_at, paid_at
            FROM payroll_periods
            WHERE start_date = $start_date AND end_date = $end_date
            LIMIT 1;
            """;
        command.AddText("$start_date", startDate.ToString("O"));
        command.AddText("$end_date", endDate.ToString("O"));

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadPeriod(reader) : null;
    }

    public PayrollPeriod? GetPeriod(Guid id)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, start_date, end_date, state, total_services, total_commission_cents, 
                   total_adjustments_cents, total_to_pay_cents, payment_method, payment_reference, 
                   notes, generated_at, paid_at
            FROM payroll_periods
            WHERE id = $id;
            """;
        command.AddText("$id", id.ToString());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadPeriod(reader) : null;
    }

    public IReadOnlyList<PayrollLine> ListLines(Guid periodId)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, period_id, barber_id, barber_name, station_number, closed_services_count, 
                   cash_generated_cents, commission_cents, adjustments_cents, total_cents
            FROM payroll_lines
            WHERE period_id = $period_id
            ORDER BY barber_name;
            """;
        command.AddText("$period_id", periodId.ToString());

        var list = new List<PayrollLine>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new PayrollLine(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Guid.Parse(reader.GetString(2)),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt64(6),
                reader.GetInt64(7),
                reader.GetInt64(8),
                reader.GetInt64(9)
            ));
        }
        return list;
    }

    public IReadOnlyList<PayrollAdjustment> ListAdjustments(Guid periodId)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, period_id, barber_id, amount_cents, reason, created_at
            FROM payroll_adjustments
            WHERE period_id = $period_id
            ORDER BY created_at;
            """;
        command.AddText("$period_id", periodId.ToString());

        var list = new List<PayrollAdjustment>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new PayrollAdjustment(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Guid.Parse(reader.GetString(2)),
                reader.GetInt64(3),
                reader.GetString(4),
                DateTimeOffset.Parse(reader.GetString(5))
            ));
        }
        return list;
    }

    public void AddAdjustment(PayrollAdjustment adjustment)
    {
        if (adjustment.Id == Guid.Empty || adjustment.PeriodId == Guid.Empty || adjustment.BarberId == Guid.Empty)
        {
            throw new ArgumentException("Adjustment, period and barber ids are required.", nameof(adjustment));
        }

        if (string.IsNullOrWhiteSpace(adjustment.Reason))
        {
            throw new ArgumentException("Adjustment reason is required.", nameof(adjustment));
        }

        var period = GetPeriod(adjustment.PeriodId)
            ?? throw new InvalidOperationException("Payroll period was not found.");
        if (period.State == PayrollPeriodState.Paid)
        {
            throw new InvalidOperationException("A paid payroll period cannot be adjusted.");
        }

        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            INSERT INTO payroll_adjustments (
                id, period_id, barber_id, amount_cents, reason, created_at
            ) VALUES (
                $id, $period_id, $barber_id, $amount_cents, $reason, $created_at
            );
            """;
        command.AddText("$id", adjustment.Id.ToString());
        command.AddText("$period_id", adjustment.PeriodId.ToString());
        command.AddText("$barber_id", adjustment.BarberId.ToString());
        command.AddInteger("$amount_cents", adjustment.AmountCents);
        command.AddText("$reason", adjustment.Reason);
        command.AddText("$created_at", adjustment.CreatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void SavePeriod(PayrollPeriod period, IEnumerable<PayrollLine> lines)
    {
        if (period.Id == Guid.Empty)
        {
            throw new ArgumentException("Payroll period id is required.", nameof(period));
        }

        var existing = GetPeriod(period.Id);
        if (existing?.State == PayrollPeriodState.Paid)
        {
            throw new InvalidOperationException("A paid payroll period cannot be recalculated.");
        }

        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            INSERT INTO payroll_periods (
                id, start_date, end_date, state, total_services, total_commission_cents, 
                total_adjustments_cents, total_to_pay_cents, payment_method, payment_reference, 
                notes, generated_at, paid_at
            ) VALUES (
                $id, $start_date, $end_date, $state, $total_services, $total_commission_cents, 
                $total_adjustments_cents, $total_to_pay_cents, $payment_method, $payment_reference, 
                $notes, $generated_at, $paid_at
            )
            ON CONFLICT(id) DO UPDATE SET
                state = excluded.state,
                total_services = excluded.total_services,
                total_commission_cents = excluded.total_commission_cents,
                total_adjustments_cents = excluded.total_adjustments_cents,
                total_to_pay_cents = excluded.total_to_pay_cents,
                payment_method = excluded.payment_method,
                payment_reference = excluded.payment_reference,
                notes = excluded.notes,
                generated_at = excluded.generated_at,
                paid_at = excluded.paid_at;
            """;
        command.AddText("$id", period.Id.ToString());
        command.AddText("$start_date", period.StartDate.ToString("O"));
        command.AddText("$end_date", period.EndDate.ToString("O"));
        command.AddInteger("$state", (int)period.State);
        command.AddInteger("$total_services", period.TotalServices);
        command.AddInteger("$total_commission_cents", period.TotalCommissionCents);
        command.AddInteger("$total_adjustments_cents", period.TotalAdjustmentsCents);
        command.AddInteger("$total_to_pay_cents", period.TotalToPayCents);
        if (period.PaymentMethod.HasValue) command.AddInteger("$payment_method", (int)period.PaymentMethod.Value); else command.Parameters.AddWithValue("$payment_method", DBNull.Value);
        command.AddText("$payment_reference", period.PaymentReference);
        command.AddText("$notes", period.Notes);
        command.AddText("$generated_at", period.GeneratedAt.ToString("O"));
        command.AddText("$paid_at", period.PaidAt?.ToString("O"));
        command.ExecuteNonQuery();

        // Eliminar lineas previas si es update de Draft
        using var deleteLinesCmd = _connection.CreateCommand();
        deleteLinesCmd.Transaction = _transaction;
        deleteLinesCmd.CommandText = "DELETE FROM payroll_lines WHERE period_id = $period_id";
        deleteLinesCmd.AddText("$period_id", period.Id.ToString());
        deleteLinesCmd.ExecuteNonQuery();

        foreach (var line in lines)
        {
            using var lineCmd = _connection.CreateCommand();
            lineCmd.Transaction = _transaction;
            lineCmd.CommandText = """
                INSERT INTO payroll_lines (
                    id, period_id, barber_id, barber_name, station_number, closed_services_count, 
                    cash_generated_cents, commission_cents, adjustments_cents, total_cents
                ) VALUES (
                    $id, $period_id, $barber_id, $barber_name, $station_number, $closed_services_count, 
                    $cash_generated_cents, $commission_cents, $adjustments_cents, $total_cents
                );
                """;
            lineCmd.AddText("$id", line.Id.ToString());
            lineCmd.AddText("$period_id", line.PeriodId.ToString());
            lineCmd.AddText("$barber_id", line.BarberId.ToString());
            lineCmd.AddText("$barber_name", line.BarberName);
            if (line.StationNumber.HasValue) lineCmd.AddInteger("$station_number", line.StationNumber.Value); else lineCmd.Parameters.AddWithValue("$station_number", DBNull.Value);
            lineCmd.AddInteger("$closed_services_count", line.ClosedServicesCount);
            lineCmd.AddInteger("$cash_generated_cents", line.CashGeneratedCents);
            lineCmd.AddInteger("$commission_cents", line.CommissionCents);
            lineCmd.AddInteger("$adjustments_cents", line.AdjustmentsCents);
            lineCmd.AddInteger("$total_cents", line.TotalCents);
            lineCmd.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<CashPayment> GetUnpaidPayments(DateTimeOffset startDate, DateTimeOffset endDate)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT p.id, p.turn_id, p.barber_id, p.service_id, p.amount_cents, p.currency, p.collected_at,
                   p.device_id, p.receipt_number, p.cash_drawer_opened, p.commission_cents,
                   p.service_price_cents, p.additional_cents
            FROM cash_payments p
            WHERE p.collected_at >= $start_date AND p.collected_at < $end_date
              AND p.commission_cents IS NOT NULL
              AND p.id NOT IN (
                  SELECT i.payment_id FROM payroll_payment_items i
                  JOIN payroll_periods per ON i.period_id = per.id
                  WHERE per.state = 1
              );
            """;
        command.AddText("$start_date", startDate.ToString("O"));
        command.AddText("$end_date", endDate.ToString("O"));

        var list = new List<CashPayment>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new CashPayment(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Guid.Parse(reader.GetString(2)),
                reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
                reader.GetInt64(4),
                reader.GetString(5),
                DateTimeOffset.Parse(reader.GetString(6)),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetInt32(9) == 1,
                reader.IsDBNull(10) ? null : reader.GetInt64(10),
                reader.IsDBNull(11) ? null : reader.GetInt64(11),
                reader.GetInt64(12)
            ));
        }
        return list;
    }

    public IReadOnlyList<CashPayment> GetPaymentsForPeriod(PayrollPeriod period, Guid barberId)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;

        if (period.State == PayrollPeriodState.Paid)
        {
            command.CommandText = """
                SELECT p.id, p.turn_id, p.barber_id, p.service_id, p.amount_cents, p.currency, p.collected_at,
                       p.device_id, p.receipt_number, p.cash_drawer_opened, p.commission_cents,
                       p.service_price_cents, p.additional_cents
                FROM cash_payments p
                JOIN payroll_payment_items i ON p.id = i.payment_id
                WHERE i.period_id = $period_id AND i.barber_id = $barber_id
                ORDER BY p.collected_at;
                """;
            command.AddText("$period_id", period.Id.ToString());
            command.AddText("$barber_id", barberId.ToString());
        }
        else
        {
            command.CommandText = """
                SELECT p.id, p.turn_id, p.barber_id, p.service_id, p.amount_cents, p.currency, p.collected_at,
                       p.device_id, p.receipt_number, p.cash_drawer_opened, p.commission_cents,
                       p.service_price_cents, p.additional_cents
                FROM cash_payments p
                WHERE p.collected_at >= $start_date AND p.collected_at < $end_date
                  AND p.barber_id = $barber_id
                  AND p.commission_cents IS NOT NULL
                  AND p.id NOT IN (
                      SELECT i.payment_id FROM payroll_payment_items i
                      JOIN payroll_periods per ON i.period_id = per.id
                      WHERE per.state = 1
                  )
                ORDER BY p.collected_at;
                """;
            command.AddText("$start_date", period.StartDate.ToString("O"));
            command.AddText("$end_date", period.EndDate.ToString("O"));
            command.AddText("$barber_id", barberId.ToString());
        }

        var list = new List<CashPayment>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new CashPayment(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Guid.Parse(reader.GetString(2)),
                reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
                reader.GetInt64(4),
                reader.GetString(5),
                DateTimeOffset.Parse(reader.GetString(6)),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetInt32(9) == 1,
                reader.IsDBNull(10) ? null : reader.GetInt64(10),
                reader.IsDBNull(11) ? null : reader.GetInt64(11),
                reader.GetInt64(12)
            ));
        }
        return list;
    }

    public void MarkAsPaid(Guid periodId, PayrollPaymentMethod method, string? reference, string? notes, DateTimeOffset paidAt, string deviceId)
    {
        var period = GetPeriod(periodId);
        if (period == null)
        {
            throw new InvalidOperationException("Payroll period was not found.");
        }

        if (period.State == PayrollPeriodState.Paid)
        {
            throw new InvalidOperationException("Payroll period is already paid.");
        }

        var payments = GetUnpaidPayments(period.StartDate, period.EndDate);

        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            UPDATE payroll_periods
            SET state = 1,
                payment_method = $payment_method,
                payment_reference = $payment_reference,
                notes = $notes,
                paid_at = $paid_at
            WHERE id = $id;
            """;
        command.AddInteger("$payment_method", (int)method);
        command.AddText("$payment_reference", reference);
        command.AddText("$notes", notes);
        command.AddText("$paid_at", paidAt.ToString("O"));
        command.AddText("$id", periodId.ToString());
        command.ExecuteNonQuery();

        foreach (var payment in payments)
        {
            using var itemCmd = _connection.CreateCommand();
            itemCmd.Transaction = _transaction;
            itemCmd.CommandText = """
                INSERT INTO payroll_payment_items (id, period_id, barber_id, payment_id)
                VALUES ($id, $period_id, $barber_id, $payment_id);
                """;
            itemCmd.AddText("$id", Guid.NewGuid().ToString());
            itemCmd.AddText("$period_id", periodId.ToString());
            itemCmd.AddText("$barber_id", payment.BarberId.ToString());
            itemCmd.AddText("$payment_id", payment.Id.ToString());
            itemCmd.ExecuteNonQuery();
        }

        var auditRepo = new AuditEventRepository(_connection, _transaction);
        auditRepo.Add(new AuditEvent(
            Guid.NewGuid(),
            paidAt,
            "PayrollPeriodPaid",
            "PayrollPeriod",
            periodId,
            JsonSerializer.Serialize(new { Method = method.ToString(), Reference = reference }),
            deviceId
        ));
    }

    private static PayrollPeriod ReadPeriod(SqliteDataReader reader)
    {
        return new PayrollPeriod(
            Guid.Parse(reader.GetString(0)),
            DateTimeOffset.Parse(reader.GetString(1)),
            DateTimeOffset.Parse(reader.GetString(2)),
            (PayrollPeriodState)reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt64(5),
            reader.GetInt64(6),
            reader.GetInt64(7),
            reader.IsDBNull(8) ? null : (PayrollPaymentMethod)reader.GetInt32(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            DateTimeOffset.Parse(reader.GetString(11)),
            reader.IsDBNull(12) ? null : DateTimeOffset.Parse(reader.GetString(12))
        );
    }
}
