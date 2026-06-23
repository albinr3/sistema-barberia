using System.Text.Json;
using Barberia.Core.Assignment;
using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Barberia.Data.Reports;
using Barberia.Data.Sync;
using Barberia.Hardware.Pos;
using Barberia.Sync.Outbox;

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
            new WindowsGraphicsCashBoxReceiptPrinter(),
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
        var now = OperationalClock.Now;
        DailyOperationCoordinator.EnsureDailyReset(_connectionFactory, now, Environment.MachineName);

        using var connection = _connectionFactory.OpenConnection();
        var services = new ServiceRepository(connection).ListActive();
        return new CashBoxSnapshot(now, services);
    }

    public CashBoxTicketLookupResult LookupTicket(string ticketNumber)
    {
        if (string.IsNullOrWhiteSpace(ticketNumber))
        {
            throw new InvalidOperationException("Scan or enter the service ticket.");
        }

        var now = OperationalClock.Now;
        DailyOperationCoordinator.EnsureDailyReset(_connectionFactory, now, Environment.MachineName);

        using var connection = _connectionFactory.OpenConnection();
        var turnRepository = new LocalTurnRepository(connection);
        var barberRepository = new LocalBarberRepository(connection);

        var (turn, barber, barberStationCode) = LoadCashBoxTicketContext(
            turnRepository,
            barberRepository,
            ticketNumber,
            now);

        return new CashBoxTicketLookupResult(
            turn.DisplayTicketNumber,
            turn.TicketNumber,
            string.IsNullOrWhiteSpace(turn.CustomerName) ? "Walk-in customer" : turn.CustomerName,
            barber.DisplayName,
            barberStationCode);
    }

    public CashBoxDepositResult CloseService(string ticketNumber, Guid serviceId, decimal additionalAmount, CustomerPaymentMethod paymentMethod, string? paymentReference)
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

        var now = OperationalClock.Now;
        var deviceId = Environment.MachineName;
        CashBoxDepositResult? result = null;

        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            DailyOperationCoordinator.EnsureDailyReset(connection, sqliteTransaction, now, deviceId);

            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var turnRepository = new LocalTurnRepository(connection, sqliteTransaction);
            var paymentRepository = new CashPaymentRepository(connection, sqliteTransaction);
            var serviceRepository = new ServiceRepository(connection, sqliteTransaction);
            var auditRepository = new AuditEventRepository(connection, sqliteTransaction);
            var dailyRotationRepository = new DailyRotationRepository(connection, sqliteTransaction);
            var appointmentRepository = new AppointmentReservationRepository(connection, sqliteTransaction);

            var receiptNumber = paymentRepository.GetNextReceiptNumber();

            var (turn, barber, barberStationCode) = LoadCashBoxTicketContext(
                turnRepository,
                barberRepository,
                ticketNumber,
                now);
            var barberId = barber.Id;
            if (!barber.IsActive)
            {
                throw new InvalidOperationException("Assigned barber is deactivated by administration.");
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
            var receiptPrinted = false;
            var cashDrawerOpened = false;
            string? hardwareFailureMessage = null;

            try
            {
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
                    deviceId,
                    paymentMethod.ToString()));
                    
                if (!printResult.Succeeded)
                {
                    hardwareFailureMessage = $"Printer failed: {printResult.ErrorMessage}";
                }
                else
                {
                    receiptPrinted = true;
                }
            }
            catch (Exception ex)
            {
                hardwareFailureMessage = $"Printer error: {ex.Message}";
            }

            if (paymentMethod == CustomerPaymentMethod.Cash)
            {
                try
                {
                    var drawerResult = _cashDrawer.Open(deviceId);
                    if (!drawerResult.Succeeded)
                    {
                        var msg = $"Drawer failed: {drawerResult.ErrorMessage}";
                        hardwareFailureMessage = hardwareFailureMessage == null ? msg : $"{hardwareFailureMessage} | {msg}";
                    }
                    else
                    {
                        cashDrawerOpened = true;
                    }
                }
                catch (Exception ex)
                {
                    var msg = $"Drawer error: {ex.Message}";
                    hardwareFailureMessage = hardwareFailureMessage == null ? msg : $"{hardwareFailureMessage} | {msg}";
                }
            }

            if (hardwareFailureMessage != null)
            {
                auditRepository.Add(new AuditEvent(
                    Guid.NewGuid(),
                    now,
                    "cash_box_hardware_failure",
                    "turn",
                    turn.Id,
                    JsonSerializer.Serialize(new { error = hardwareFailureMessage, receiptNumber }),
                    deviceId));
            }

            var barbers = barberRepository
                .ListAll()
                .Where(candidate => candidate.IsActive)
                .ToArray();
            var businessDate = DailyOperationCoordinator.GetBusinessDate(now);
            var rotationQueue = DailyRotationQueue.Build(
                barbers,
                dailyRotationRepository.ListByDate(businessDate),
                businessDate);
            var closeResult = _assignmentEngine.CloseServiceAtCashBox(
                new CashBoxCloseRequest(barberId, rotationQueue));

            var paymentId = Guid.NewGuid();
            paymentRepository.Add(new CashPayment(
                paymentId,
                turn.Id,
                barberId,
                service.Id,
                amount,
                Currency,
                now,
                deviceId,
                receiptNumber,
                cashDrawerOpened: cashDrawerOpened,
                commission,
                servicePrice,
                additionalAmount,
                paymentMethod,
                paymentReference));
            turnRepository.MarkCompleted(turn.Id, now);
            if (turn.AppointmentId is Guid appointmentId)
            {
                appointmentRepository.MarkCompleted(appointmentId, now, now);
            }
            barberRepository.ApplyCashBoxClose(
                closeResult.BarberId,
                closeResult.BarberState,
                now);
            dailyRotationRepository.MoveToEnd(businessDate, closeResult.BarberId, barber.CheckedInAt ?? now, now);

            var syncRecorder = new SyncOutboxRecorder(new SyncOutboxRepository(connection, sqliteTransaction));

            syncRecorder.Enqueue(new LocalSyncEvent(
                Guid.NewGuid(), now, "ticket.completed", "ticket", turn.Id,
                JsonSerializer.Serialize(TicketSyncPayload.Create(
                    turn,
                    "completed",
                    barberId,
                    now,
                    new[] { new { service_id = service.Id, price_cents = service.PriceCents, local_item_id = Guid.NewGuid().ToString() } })),
                deviceId), now);

            syncRecorder.Enqueue(new LocalSyncEvent(
                Guid.NewGuid(), now, "payment.collected", "payment", paymentId,
                JsonSerializer.Serialize(new {
                    ticket_id = turn.Id,
                    appointment_id = turn.AppointmentId,
                    payment_method = paymentMethod.ToString().ToLower(),
                    amount_cents = Money.ToCents(amount),
                    receipt_number = receiptNumber,
                    payment_reference = paymentReference,
                    collected_at = now
                }),
                deviceId), now);

            if (turn.AppointmentId is Guid completedAppointmentId)
            {
                syncRecorder.Enqueue(new LocalSyncEvent(
                    Guid.NewGuid(), now, "appointment.completed", "appointment", completedAppointmentId,
                    JsonSerializer.Serialize(new
                    {
                        appointment_id = completedAppointmentId,
                        appointment_code = turn.TicketNumber,
                        ticket_id = turn.Id,
                        completed_at = now
                    }),
                    deviceId), now);
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
                    receiptPrinted = receiptPrinted,
                    cashDrawerOpened = cashDrawerOpened,
                    paymentMethod = paymentMethod.ToString(),
                    paymentReference
                }),
                deviceId));

            // After barber becomes available, try to assign the next waiting turn
            var reassignment = TryAssignNextWaitingTurn(
                turnRepository,
                barberRepository,
                dailyRotationRepository,
                appointmentRepository,
                now);

            if (reassignment is not null)
            {
                syncRecorder.Enqueue(new LocalSyncEvent(
                    Guid.NewGuid(), now, "ticket.called", "ticket", reassignment.TurnId,
                    JsonSerializer.Serialize(TicketSyncPayload.Create(
                        turnRepository.GetById(reassignment.TurnId)
                            ?? new Turn(
                                reassignment.TurnId,
                                reassignment.TicketNumber,
                                reassignment.DisplayTicketNumber,
                                businessDate,
                                TurnState.Called,
                                TurnSource.WalkIn,
                                now,
                                reassignment.BarberId),
                        "called",
                        reassignment.BarberId)),
                    deviceId), now);

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
                paymentMethod == CustomerPaymentMethod.Zelle ? "Service closed by Zelle. Payment registered." : "Service closed locally. Deposit cash into cash drawer.",
                receiptPrinted,
                cashDrawerOpened,
                hardwareFailureMessage);
        });

        return result ?? throw new InvalidOperationException("Could not close service at cash box.");
    }

    public void PrintDayReport()
    {
        var now = OperationalClock.Now;
        var deviceId = Environment.MachineName;
        var businessDate = DailyOperationCoordinator.GetBusinessDate(now);
        var from = OperationalClock.StartOfDay(businessDate);
        var to = OperationalClock.StartOfDay(businessDate.AddDays(1));

        using var connection = _connectionFactory.OpenConnection();
        var report = new LocalAdminReportRepository(connection).Load(from, to, now);

        var barberReports = report.Barbers
            .Select(b => new BarberDayReport(b.DisplayNameWithStation, b.ServicesClosed, b.CashCollectedCents / 100m))
            .ToList();

        var job = new DayReportPrintJob(
            report.Cash.TotalSalesCents / 100m,
            barberReports,
            now,
            deviceId);

        var result = _receiptPrinter.PrintDayReport(job);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not print day report: {result.ErrorMessage}");
        }
    }

    private static (Turn Turn, Barber Barber, string BarberStationCode) LoadCashBoxTicketContext(
        LocalTurnRepository turnRepository,
        LocalBarberRepository barberRepository,
        string ticketNumber,
        DateTimeOffset now)
    {
        var turn = turnRepository.GetByTicketInputForToday(ticketNumber, now)
            ?? throw new InvalidOperationException("Ticket not found in local database.");

        var barberId = turn.AssignedBarberId
            ?? throw new InvalidOperationException("The ticket has no assigned barber.");
        var barber = barberRepository.GetById(barberId)
            ?? throw new InvalidOperationException("Assigned barber does not exist in local database.");
        var barberStationCode = barber.StationCode
            ?? throw new InvalidOperationException("Active barber has no assigned station.");

        if (turn.State == TurnState.Completed)
        {
            throw new InvalidOperationException($"This ticket was already completed and charged by {barber.DisplayName}.");
        }

        if (turn.State != TurnState.InService || barber.State != BarberState.InService)
        {
            throw new InvalidOperationException("This ticket is not currently being attended.");
        }

        return (turn, barber, barberStationCode);
    }

    private TurnAssignmentDecision? TryAssignNextWaitingTurn(
        LocalTurnRepository turnRepository,
        LocalBarberRepository barberRepository,
        DailyRotationRepository dailyRotationRepository,
        AppointmentReservationRepository appointmentRepository,
        DateTimeOffset now)
    {
        var barbers = barberRepository
            .ListAll()
            .Where(barber => barber.IsActive)
            .ToArray();
        var waitingTurns = turnRepository.ListWaiting();
        var businessDate = DailyOperationCoordinator.GetBusinessDate(now);
        var rotationQueue = DailyRotationQueue.Build(
            barbers,
            dailyRotationRepository.ListByDate(businessDate),
            businessDate);
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
