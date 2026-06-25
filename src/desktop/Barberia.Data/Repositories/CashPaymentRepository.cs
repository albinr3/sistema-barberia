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
        command.AddText("$turn_id", SqliteForeignKeyIds.ExistingId(_connection, _transaction, "turns", payment.TurnId));
        command.AddText("$barber_id", SqliteForeignKeyIds.ExistingId(_connection, _transaction, "barbers", payment.BarberId));
        command.AddText("$service_id", payment.ServiceId is null
            ? null
            : SqliteForeignKeyIds.ExistingId(_connection, _transaction, "services", payment.ServiceId.Value));
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

    public IReadOnlyList<ReceiptPrintRecord> ListReceiptsForReprint(DateTimeOffset businessDate, string? searchQuery = null)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        
        var query = """
            SELECT p.id,
                   p.receipt_number,
                   t.display_ticket_number,
                   t.ticket_number,
                   b.display_name,
                   b.station_number,
                   s.name as service_name,
                   p.service_price_cents,
                   p.additional_cents,
                   p.amount_cents,
                   p.commission_cents,
                   p.currency,
                   p.collected_at,
                   p.device_id,
                   p.payment_method
            FROM cash_payments p
            JOIN turns t ON p.turn_id = t.id
            JOIN barbers b ON p.barber_id = b.id
            LEFT JOIN services s ON REPLACE(s.id, '-', '') = REPLACE(p.service_id, '-', '')
            WHERE t.ticket_date = $business_date
            """;
            
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            query += " AND (t.display_ticket_number = $search OR t.ticket_number = $search OR p.receipt_number = $search)";
        }
        
        query += " ORDER BY p.collected_at DESC;";
        
        command.CommandText = query;
        command.AddText("$business_date", businessDate.ToString("yyyy-MM-dd"));
        
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            command.AddText("$search", searchQuery.Trim());
        }

        using var reader = command.ExecuteReader();
        var receipts = new List<ReceiptPrintRecord>();
        while (reader.Read())
        {
            receipts.Add(new ReceiptPrintRecord(
                Guid.Parse(reader.GetString(0)),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? "Unknown" : reader.GetInt32(5).ToString(),
                reader.IsDBNull(6) ? "Unknown" : reader.GetString(6),
                (reader.IsDBNull(7) ? 0m : reader.GetInt64(7) / 100m),
                (reader.GetInt64(8) / 100m),
                (reader.GetInt64(9) / 100m),
                (reader.IsDBNull(10) ? 0m : reader.GetInt64(10) / 100m),
                reader.GetString(11),
                DateTimeOffset.Parse(reader.GetString(12)),
                reader.GetString(13),
                (CustomerPaymentMethod)reader.GetInt32(14)));
        }

        return receipts;
    }
}
