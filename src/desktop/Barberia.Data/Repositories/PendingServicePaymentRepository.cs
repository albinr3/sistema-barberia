using Barberia.Data.Models;
using Microsoft.Data.Sqlite;

namespace Barberia.Data.Repositories;

public sealed class PendingServicePaymentRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;

    public PendingServicePaymentRepository(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public void Add(PendingServicePayment payment)
    {
        if (payment.Id == Guid.Empty || payment.TurnId == Guid.Empty || payment.BarberId == Guid.Empty || payment.ServiceId == Guid.Empty)
        {
            throw new ArgumentException("Pending payment, turn, barber and service ids are required.", nameof(payment));
        }

        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            INSERT INTO pending_service_payments (
                id, turn_id, barber_id, service_id, business_date, service_price_cents,
                additional_cents, amount_cents, commission_cents, currency, device_id,
                pending_at, paid_at, voided_at, receipt_number, payment_method, payment_reference
            ) VALUES (
                $id, $turn_id, $barber_id, $service_id, $business_date, $service_price_cents,
                $additional_cents, $amount_cents, $commission_cents, $currency, $device_id,
                $pending_at, $paid_at, $voided_at, $receipt_number, $payment_method, $payment_reference
            );
            """;
        command.AddText("$id", payment.Id.ToString());
        command.AddText("$turn_id", SqliteForeignKeyIds.ExistingId(_connection, _transaction, "turns", payment.TurnId));
        command.AddText("$barber_id", SqliteForeignKeyIds.ExistingId(_connection, _transaction, "barbers", payment.BarberId));
        command.AddText("$service_id", SqliteForeignKeyIds.ExistingId(_connection, _transaction, "services", payment.ServiceId));
        command.AddText("$business_date", payment.BusinessDate.ToString("yyyy-MM-dd"));
        command.AddInteger("$service_price_cents", payment.ServicePriceCents);
        command.AddInteger("$additional_cents", payment.AdditionalCents);
        command.AddInteger("$amount_cents", payment.AmountCents);
        command.AddInteger("$commission_cents", payment.CommissionCents);
        command.AddText("$currency", payment.Currency);
        command.AddText("$device_id", payment.DeviceId);
        command.AddText("$pending_at", payment.PendingAt.ToString("O"));
        command.AddText("$paid_at", payment.PaidAt?.ToString("O"));
        command.AddText("$voided_at", payment.VoidedAt?.ToString("O"));
        command.AddText("$receipt_number", payment.ReceiptNumber);
        command.Parameters.AddWithValue("$payment_method", payment.PaymentMethod.HasValue ? (int)payment.PaymentMethod.Value : DBNull.Value);
        command.Parameters.AddWithValue("$payment_reference", payment.PaymentReference is null ? DBNull.Value : payment.PaymentReference);
        command.ExecuteNonQuery();
    }

    public int CountOpenByBusinessDate(DateOnly businessDate)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT COUNT(*)
            FROM pending_service_payments
            WHERE business_date = $business_date
              AND paid_at IS NULL
              AND voided_at IS NULL;
            """;
        command.AddText("$business_date", businessDate.ToString("yyyy-MM-dd"));
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public IReadOnlyList<PendingServicePaymentRow> ListOpenByBusinessDate(DateOnly businessDate)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = OpenPendingSelectSql + """
            WHERE p.business_date = $business_date
              AND p.paid_at IS NULL
              AND p.voided_at IS NULL
            ORDER BY p.pending_at, t.display_ticket_number, p.id;
            """;
        command.AddText("$business_date", businessDate.ToString("yyyy-MM-dd"));
        return ReadRows(command);
    }

    public IReadOnlyList<PendingServicePaymentRow> GetOpenByIds(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        var placeholders = ids.Select((id, index) =>
        {
            var parameterName = $"$id{index}";
            command.AddText(parameterName, id.ToString());
            return parameterName;
        });

        command.CommandText = OpenPendingSelectSql + $"""
            WHERE p.id IN ({string.Join(", ", placeholders)})
              AND p.paid_at IS NULL
              AND p.voided_at IS NULL
            ORDER BY p.pending_at, t.display_ticket_number, p.id;
            """;

        return ReadRows(command);
    }

    public void MarkPaid(Guid id, string receiptNumber, CustomerPaymentMethod paymentMethod, string? paymentReference, DateTimeOffset paidAt)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            UPDATE pending_service_payments
            SET paid_at = $paid_at,
                receipt_number = $receipt_number,
                payment_method = $payment_method,
                payment_reference = $payment_reference
            WHERE id = $id
              AND paid_at IS NULL
              AND voided_at IS NULL;
            """;
        command.AddText("$id", id.ToString());
        command.AddText("$paid_at", paidAt.ToString("O"));
        command.AddText("$receipt_number", receiptNumber);
        command.AddInteger("$payment_method", (int)paymentMethod);
        command.Parameters.AddWithValue("$payment_reference", paymentReference is null ? DBNull.Value : paymentReference);

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("Pending payment was already collected or no longer exists.");
        }
    }

    private static IReadOnlyList<PendingServicePaymentRow> ReadRows(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var rows = new List<PendingServicePaymentRow>();
        while (reader.Read())
        {
            rows.Add(new PendingServicePaymentRow(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Guid.Parse(reader.GetString(2)),
                Guid.Parse(reader.GetString(3)),
                DateOnly.Parse(reader.GetString(4)),
                reader.GetInt32(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? "Walk-in customer" : reader.GetString(7),
                reader.GetString(8),
                reader.IsDBNull(9) ? "B-?" : $"B-{reader.GetInt32(9)}",
                reader.GetString(10),
                reader.GetInt64(11),
                reader.GetInt64(12),
                reader.GetInt64(13),
                reader.GetInt64(14),
                reader.GetString(15),
                DateTimeOffset.Parse(reader.GetString(16))));
        }

        return rows;
    }

    private const string OpenPendingSelectSql = """
        SELECT p.id,
               p.turn_id,
               p.barber_id,
               p.service_id,
               p.business_date,
               t.display_ticket_number,
               t.ticket_number,
               t.customer_name,
               b.display_name,
               b.station_number,
               s.name,
               p.service_price_cents,
               p.additional_cents,
               p.amount_cents,
               p.commission_cents,
               p.currency,
               p.pending_at
        FROM pending_service_payments p
        JOIN turns t ON REPLACE(LOWER(t.id), '-', '') = REPLACE(LOWER(p.turn_id), '-', '')
        JOIN barbers b ON REPLACE(LOWER(b.id), '-', '') = REPLACE(LOWER(p.barber_id), '-', '')
        JOIN services s ON REPLACE(LOWER(s.id), '-', '') = REPLACE(LOWER(p.service_id), '-', '')
        """;
}
