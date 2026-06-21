using System.Text.Json;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Barberia.Hardware.Pos;

namespace Barberia.Desktop.Services;

public sealed class ReceiptReprintService
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ICashBoxReceiptPrinter _receiptPrinter;

    public ReceiptReprintService()
        : this(LocalDesktopDatabase.CreateConnectionFactory(), new WindowsGraphicsCashBoxReceiptPrinter())
    {
    }

    public ReceiptReprintService(SqliteConnectionFactory connectionFactory, ICashBoxReceiptPrinter receiptPrinter)
    {
        _connectionFactory = connectionFactory;
        _receiptPrinter = receiptPrinter;
    }

    public IReadOnlyList<ReceiptPrintRecord> SearchReceipts(DateTimeOffset businessDate, string? searchQuery = null)
    {
        using var connection = _connectionFactory.OpenConnection();
        var repository = new CashPaymentRepository(connection);
        return repository.ListReceiptsForReprint(businessDate, searchQuery);
    }

    public void ReprintReceipt(ReceiptPrintRecord record)
    {
        var now = OperationalClock.Now;
        var deviceId = Environment.MachineName;

        if (string.IsNullOrWhiteSpace(record.ReceiptNumber))
        {
            throw new InvalidOperationException("Receipt number is required to reprint a receipt.");
        }

        var job = new CashReceiptPrintJob(
            record.ReceiptNumber,
            record.DisplayTicketNumber,
            record.BarberName,
            record.BarberStationCode,
            record.ServiceName,
            record.ServicePrice,
            record.AdditionalAmount,
            record.TotalAmount,
            record.Commission,
            record.Currency,
            record.CollectedAt,
            deviceId,
            record.PaymentMethod.ToString() + " (REPRINT)");

        var printResult = _receiptPrinter.Print(job);

        using var connection = _connectionFactory.OpenConnection();
        var auditRepository = new AuditEventRepository(connection);

        if (!printResult.Succeeded)
        {
            auditRepository.Add(new AuditEvent(
                Guid.NewGuid(),
                now,
                "receipt_reprint_failed",
                "payment",
                record.PaymentId,
                JsonSerializer.Serialize(new { record.ReceiptNumber, error = printResult.ErrorMessage }),
                deviceId));
                
            throw new InvalidOperationException($"Could not print receipt: {printResult.ErrorMessage}");
        }

        auditRepository.Add(new AuditEvent(
            Guid.NewGuid(),
            now,
            "receipt_reprinted",
            "payment",
            record.PaymentId,
            JsonSerializer.Serialize(new { record.ReceiptNumber }),
            deviceId));
    }
}
