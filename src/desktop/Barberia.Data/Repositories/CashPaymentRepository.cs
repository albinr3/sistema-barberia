using Barberia.Data.Models;
using Microsoft.Data.Sqlite;

namespace Barberia.Data.Repositories;

public sealed class CashPaymentRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;

    public CashPaymentRepository(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public void Add(CashPayment payment)
    {
        if (payment.Id == Guid.Empty || payment.TurnId == Guid.Empty || payment.BarberId == Guid.Empty)
        {
            throw new ArgumentException("Payment, turn and barber ids are required.", nameof(payment));
        }

        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            INSERT INTO cash_payments (
                id, turn_id, barber_id, service_id, amount_cents, currency, collected_at,
                device_id, receipt_number, cash_drawer_opened, commission_cents,
                service_price_cents, additional_cents, payment_method, payment_reference
            ) VALUES (
                $id, $turn_id, $barber_id, $service_id, $amount_cents, $currency, $collected_at,
                $device_id, $receipt_number, $cash_drawer_opened, $commission_cents,
                $service_price_cents, $additional_cents, $payment_method, $payment_reference
            );
            """;
        command.AddText("$id", payment.Id.ToString());
        command.AddText("$turn_id", payment.TurnId.ToString());
        command.AddText("$barber_id", payment.BarberId.ToString());
        command.AddText("$service_id", payment.ServiceId?.ToString());
        command.AddInteger("$amount_cents", payment.AmountCents);
        command.AddText("$currency", payment.Currency);
        command.AddText("$collected_at", payment.CollectedAt.ToString("O"));
        command.AddText("$device_id", payment.DeviceId);
        command.AddText("$receipt_number", payment.ReceiptNumber);
        command.Parameters.AddWithValue("$cash_drawer_opened", payment.CashDrawerOpened ? 1 : 0);
        command.Parameters.AddWithValue("$commission_cents", payment.CommissionCents is null ? DBNull.Value : payment.CommissionCents);
        command.Parameters.AddWithValue("$service_price_cents", payment.ServicePriceCents is null ? DBNull.Value : payment.ServicePriceCents);
        command.AddInteger("$additional_cents", payment.AdditionalCents);
        command.Parameters.AddWithValue("$payment_method", (int)payment.PaymentMethod);
        command.Parameters.AddWithValue("$payment_reference", payment.PaymentReference is null ? DBNull.Value : payment.PaymentReference);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<CashPayment> ListByTurn(Guid turnId)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, turn_id, barber_id, service_id, amount_cents, currency, collected_at,
                   device_id, receipt_number, cash_drawer_opened, commission_cents,
                   service_price_cents, additional_cents, payment_method, payment_reference
            FROM cash_payments
            WHERE turn_id = $turn_id
            ORDER BY collected_at;
            """;
        command.AddText("$turn_id", turnId.ToString());

        using var reader = command.ExecuteReader();
        var payments = new List<CashPayment>();
        while (reader.Read())
        {
            payments.Add(new CashPayment(
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
                reader.GetInt64(12),
                (CustomerPaymentMethod)reader.GetInt32(13),
                reader.IsDBNull(14) ? null : reader.GetString(14)));
        }

        return payments;
    }

    public string GetNextReceiptNumber()
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT receipt_number
            FROM cash_payments
            WHERE receipt_number LIKE 'CB-%'
              AND length(receipt_number) = 10
            ORDER BY receipt_number DESC
            LIMIT 1;
            """;
        var lastReceipt = command.ExecuteScalar() as string;

        if (lastReceipt != null && lastReceipt.StartsWith("CB-") && int.TryParse(lastReceipt.Substring(3), out var lastNumber))
        {
            return $"CB-{(lastNumber + 1):D7}";
        }

        return "CB-0000001";
    }
}
