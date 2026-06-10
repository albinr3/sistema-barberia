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
        var services = new ServiceRepository(connection).ListActive();
        return new CashBoxSnapshot(DateTimeOffset.Now, services);
    }

    public CashBoxTicketLookupResult LookupTicket(string ticketNumber)
    {
        if (string.IsNullOrWhiteSpace(ticketNumber))
        {
            throw new InvalidOperationException("Scan or enter the service ticket.");
        }

        using var connection = _connectionFactory.OpenConnection();
        var turnRepository = new LocalTurnRepository(connection);
        var barberRepository = new LocalBarberRepository(connection);
        var now = DateTimeOffset.Now;

        var turn = turnRepository.GetByTicketInputForToday(ticketNumber, now)
            ?? throw new InvalidOperationException("Ticket not found in local database.");

        var barberId = turn.AssignedBarberId
            ?? throw new InvalidOperationException("The ticket has no assigned barber.");
        var barber = barberRepository.GetById(barberId)
            ?? throw new InvalidOperationException("Assigned barber does not exist in local database.");
        var barberStationCode = barber.StationCode
            ?? throw new InvalidOperationException("Active barber has no assigned station.");

        return new CashBoxTicketLookupResult(
            turn.DisplayTicketNumber,
            turn.TicketNumber,
            string.IsNullOrWhiteSpace(turn.CustomerName) ? "Walk-in customer" : turn.CustomerName,
            barber.DisplayName,
            barberStationCode);
    }

    public CashBoxDepositResult CloseService(string ticketNumber, Guid serviceId, decimal additionalAmount)
    {
        if (string.IsNullOrWhiteSpace(ticketNumber))
        {
            throw new InvalidOperationException("Scan or enter the service ticket.");
        }

        if (serviceId == Guid.Empty)
        {
            throw new InvalidOperationException("Select the provided service.");
        }

        if (additionalAmount is not (0m or 2m or 3m or 5m))
        {
            throw new InvalidOperationException("The addition must be 0, 2, 3 or 5 dollars.");
        }

        var now = DateTimeOffset.Now;
        var deviceId = Environment.MachineName;
        CashBoxDepositResult? result = null;

        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var turnRepository = new LocalTurnRepository(connection, sqliteTransaction);
            var paymentRepository = new CashPaymentRepository(connection, sqliteTransaction);
            var serviceRepository = new ServiceRepository(connection, sqliteTransaction);
            var auditRepository = new AuditEventRepository(connection, sqliteTransaction);

            var receiptNumber = paymentRepository.GetNextReceiptNumber();

            var turn = turnRepository.GetByTicketInputForToday(ticketNumber, now)
                ?? throw new InvalidOperationException("Ticket not found in local database.");

            var barberId = turn.AssignedBarberId
                ?? throw new InvalidOperationException("The ticket has no assigned barber.");
            var barber = barberRepository.GetById(barberId)
                ?? throw new InvalidOperationException("Assigned barber does not exist in local database.");
            if (!barber.IsActive)
            {
                throw new InvalidOperationException("Assigned barber is deactivated by administration.");
            }

            if (turn.State != TurnState.InService || barber.State != BarberState.InService)
            {
                throw new InvalidOperationException("Ticket and barber must be in service to close at cash box.");
            }

            var service = serviceRepository.GetById(serviceId)
                ?? throw new InvalidOperationException("Service not found in local database.");
            if (!service.IsActive)
            {
                throw new InvalidOperationException("This service is deactivated by administration.");
            }

            var servicePrice = service.Price;
            if (servicePrice <= 0)
            {
                throw new InvalidOperationException("The service base price must be greater than zero.");
            }

            var amount = servicePrice + additionalAmount;
            var commission = decimal.Round(amount * barber.CommissionRate, 2, MidpointRounding.AwayFromZero);
            var barberStationCode = barber.StationCode
                ?? throw new InvalidOperationException("Active barber has no assigned station.");
            var printResult = _receiptPrinter.Print(new CashReceiptPrintJob(
                receiptNumber,
                turn.DisplayTicketNumber,
                barber.DisplayName,
                barberStationCode,
                service.Name,
                servicePrice,
                additionalAmount,
                amount,
                commission,
                Currency,
                now,
                deviceId));
            if (!printResult.Succeeded)
            {
                throw new InvalidOperationException($"Could not print receipt: {printResult.ErrorMessage}");
            }

            var drawerResult = _cashDrawer.Open(deviceId);
            if (!drawerResult.Succeeded)
            {
                throw new InvalidOperationException($"Could not open cash drawer: {drawerResult.ErrorMessage}");
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
                service.Id,
                amount,
                Currency,
                now,
                deviceId,
                receiptNumber,
                cashDrawerOpened: true,
                commission,
                servicePrice,
                additionalAmount));
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
                    serviceId = service.Id,
                    serviceName = service.Name,
                    servicePrice,
                    additionalAmount,
                    amount,
                    currency = Currency,
                    commission,
                    commissionPercentage = barber.CommissionPercentage,
                    commissionRate = barber.CommissionRate,
                    receiptNumber,
                    receiptPrinted = true,
                    cashDrawerOpened = true
                }),
                deviceId));

            // After barber becomes available, try to assign the next waiting turn
            var reassignment = TryAssignNextWaitingTurn(
                turnRepository,
                barberRepository,
                new AppointmentReservationRepository(connection, sqliteTransaction),
                now);

            if (reassignment is not null)
            {
                auditRepository.Add(new AuditEvent(
                    Guid.NewGuid(),
                    now,
                    "cash_box_waiting_turn_assigned",
                    "turn",
                    reassignment.TurnId,
                    JsonSerializer.Serialize(new
                    {
                        turnId = reassignment.TurnId,
                        displayTicketNumber = reassignment.DisplayTicketNumber,
                        internalTicketNumber = reassignment.TicketNumber,
                        barberId = reassignment.BarberId,
                        turnState = reassignment.TurnState.ToString(),
                        barberState = reassignment.BarberState.ToString(),
                        reason = "cash_box_closed"
                    }),
                    deviceId));
            }

            result = new CashBoxDepositResult(
                turn.DisplayTicketNumber,
                turn.TicketNumber,
                barber.DisplayName,
                barberStationCode,
                service.Name,
                servicePrice,
                additionalAmount,
                amount,
                commission,
                receiptNumber,
                now,
                "Service closed locally. Deposit cash into cash drawer.");
        });

        return result ?? throw new InvalidOperationException("Could not close service at cash box.");
    }

    private TurnAssignmentDecision? TryAssignNextWaitingTurn(
        LocalTurnRepository turnRepository,
        LocalBarberRepository barberRepository,
        AppointmentReservationRepository appointmentRepository,
        DateTimeOffset now)
    {
        var barbers = barberRepository
            .ListAll()
            .Where(barber => barber.IsActive)
            .ToArray();
        var waitingTurns = turnRepository.ListWaiting();
        var rotationQueue = barbers
            .OrderBy(barber => barber.RotationOrder)
            .Select(barber => barber.Id)
            .ToArray();
        var appointments = appointmentRepository.ListBetween(now.AddMinutes(-1), now.AddMinutes(15));

        try
        {
            var decision = _assignmentEngine.AssignNextTurn(new TurnAssignmentRequest(
                waitingTurns,
                barbers,
                rotationQueue,
                now,
                appointments));

            turnRepository.ApplyAssignment(decision.TurnId, decision.BarberId, decision.TurnState, now);
            barberRepository.ApplyAssignment(decision.BarberId, decision.BarberState, now);

            return decision;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

}
