using System.Globalization;
using System.Text.Json;
using Barberia.Core.Assignment;
using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Barberia.Hardware.Pos;

namespace Barberia.Desktop.Services;

public sealed class CashBoxCloseService
{
    private const string Currency = "USD";
    private const decimal CommissionRate = 0.20m;

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ICashBoxReceiptPrinter _receiptPrinter;
    private readonly ICashDrawer _cashDrawer;
    private readonly TurnAssignmentEngine _assignmentEngine = new();

    public CashBoxCloseService()
        : this(
            LocalDesktopDatabase.CreateConnectionFactory(),
            new SimulatedCashBoxReceiptPrinter(),
            new SimulatedCashDrawer())
    {
    }

    public CashBoxCloseService(
        SqliteConnectionFactory connectionFactory,
        ICashBoxReceiptPrinter receiptPrinter,
        ICashDrawer cashDrawer)
    {
        _connectionFactory = connectionFactory;
        _receiptPrinter = receiptPrinter;
        _cashDrawer = cashDrawer;
        new LocalDatabaseInitializer(_connectionFactory).Initialize();
    }

    public CashBoxSnapshot Load()
    {
        using var connection = _connectionFactory.OpenConnection();
        var barbers = new LocalBarberRepository(connection)
            .ListAll()
            .Where(barber => barber.IsActive)
            .ToArray();
        return new CashBoxSnapshot(DateTimeOffset.Now, barbers);
    }

    public CashBoxDepositResult CloseService(Guid barberId, string ticketNumber, string amountText)
    {
        if (barberId == Guid.Empty)
        {
            throw new InvalidOperationException("Selecciona el barbero que esta cerrando el servicio.");
        }

        if (string.IsNullOrWhiteSpace(ticketNumber))
        {
            throw new InvalidOperationException("Escanea o introduce el ticket del servicio.");
        }

        if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
            && !decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            throw new InvalidOperationException("Introduce un monto en efectivo valido.");
        }

        if (amount <= 0)
        {
            throw new InvalidOperationException("El monto cobrado debe ser mayor que cero.");
        }

        var now = DateTimeOffset.Now;
        var deviceId = Environment.MachineName;
        var receiptNumber = CreateReceiptNumber(now);
        var commission = decimal.Round(amount * CommissionRate, 2, MidpointRounding.AwayFromZero);
        CashBoxDepositResult? result = null;

        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var turnRepository = new LocalTurnRepository(connection, sqliteTransaction);
            var paymentRepository = new CashPaymentRepository(connection, sqliteTransaction);
            var auditRepository = new AuditEventRepository(connection, sqliteTransaction);

            var barber = barberRepository.GetById(barberId)
                ?? throw new InvalidOperationException("Barbero no encontrado en la base local.");
            if (!barber.IsActive)
            {
                throw new InvalidOperationException("Este barbero esta desactivado por administracion.");
            }

            var turn = turnRepository.GetByTicketInputForToday(ticketNumber, now)
                ?? throw new InvalidOperationException("Ticket no encontrado en la base local.");

            if (turn.AssignedBarberId != barberId)
            {
                throw new InvalidOperationException("El ticket no pertenece al barbero seleccionado.");
            }

            if (turn.State != TurnState.InService || barber.State != BarberState.InService)
            {
                throw new InvalidOperationException("El ticket y el barbero deben estar en servicio para cerrar en autocaja.");
            }

            var barberStationCode = barber.StationCode
                ?? throw new InvalidOperationException("El barbero activo no tiene estacion asignada.");
            var printResult = _receiptPrinter.Print(new CashReceiptPrintJob(
                receiptNumber,
                turn.DisplayTicketNumber,
                barber.DisplayName,
                barberStationCode,
                amount,
                commission,
                Currency,
                now,
                deviceId));
            if (!printResult.Succeeded)
            {
                throw new InvalidOperationException($"No se pudo imprimir la constancia: {printResult.ErrorMessage}");
            }

            var drawerResult = _cashDrawer.Open(deviceId);
            if (!drawerResult.Succeeded)
            {
                throw new InvalidOperationException($"No se pudo abrir el cash drawer: {drawerResult.ErrorMessage}");
            }

            var barbers = barberRepository
                .ListAll()
                .Where(candidate => candidate.IsActive)
                .ToArray();
            var rotationQueue = barbers
                .OrderBy(candidate => candidate.RotationOrder)
                .Select(candidate => candidate.Id)
                .ToArray();
            var closeResult = _assignmentEngine.CloseServiceAtCashBox(
                new CashBoxCloseRequest(barberId, rotationQueue));

            paymentRepository.Add(new CashPayment(
                Guid.NewGuid(),
                turn.Id,
                barberId,
                amount,
                Currency,
                now,
                deviceId,
                receiptNumber,
                cashDrawerOpened: true,
                commission));
            turnRepository.MarkCompleted(turn.Id, now);
            barberRepository.ApplyCashBoxClose(
                closeResult.BarberId,
                closeResult.BarberState,
                closeResult.RotationQueue.Count - 1,
                now);

            for (var index = 0; index < closeResult.RotationQueue.Count; index++)
            {
                barberRepository.SetRotationOrder(closeResult.RotationQueue[index], index, now);
            }

            auditRepository.Add(new AuditEvent(
                Guid.NewGuid(),
                now,
                "cash_box_closed",
                "turn",
                turn.Id,
                JsonSerializer.Serialize(new
                {
                    displayTicketNumber = turn.DisplayTicketNumber,
                    internalTicketNumber = turn.TicketNumber,
                    barberId,
                    barberStationCode,
                    amount,
                    currency = Currency,
                    commission,
                    commissionRate = CommissionRate,
                    receiptNumber,
                    receiptPrinted = true,
                    cashDrawerOpened = true
                }),
                deviceId));

            result = new CashBoxDepositResult(
                turn.DisplayTicketNumber,
                turn.TicketNumber,
                barber.DisplayName,
                barberStationCode,
                amount,
                commission,
                receiptNumber,
                now,
                "Servicio cerrado localmente. Deposita el efectivo en el cash drawer.");
        });

        return result ?? throw new InvalidOperationException("No se pudo cerrar el servicio en autocaja.");
    }

    private static string CreateReceiptNumber(DateTimeOffset timestamp)
    {
        return $"CB-{timestamp:yyyyMMddHHmmssfff}";
    }
}
