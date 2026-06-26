using Barberia.Core.Assignment;
using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Barberia.Data.Sync;
using Barberia.Sync.Outbox;
using System.Text.Json;

namespace Barberia.Desktop.Services;

public sealed class BarberPanelService
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly TurnAssignmentEngine _assignmentEngine = new();

    public BarberPanelService()
        : this(LocalDesktopDatabase.CreateConnectionFactory())
    {
    }

    public BarberPanelService(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        new LocalDatabaseInitializer(_connectionFactory).Initialize();
    }

    public BarberPanelSnapshot Load(Guid? selectedBarberId)
    {
        var now = OperationalClock.Now;
        DailyOperationCoordinator.EnsureDailyReset(_connectionFactory, now, Environment.MachineName);
        var businessDate = DailyOperationCoordinator.GetBusinessDate(now);

        using var connection = _connectionFactory.OpenConnection();
        var barberRepository = new LocalBarberRepository(connection);
        var turnRepository = new LocalTurnRepository(connection);
        var dailyRotationEntries = new DailyRotationRepository(connection).ListByDate(businessDate);
        var barbers = DailyRotationQueue.OrderBarbers(
            barberRepository
            .ListAll()
            .Where(barber => barber.IsActive),
            dailyRotationEntries,
            businessDate);
        var assignedTurns = selectedBarberId is null
            ? turnRepository.ListAssignedCalled()
            : turnRepository.ListAssignedToBarber(selectedBarberId.Value);

        return new BarberPanelSnapshot(now, barbers, assignedTurns);
    }

    public void MarkAvailable(Guid barberId)
    {
        UpdateOperationalState(barberId, BarberState.Available);
    }

    public void MarkOffline(Guid barberId)
    {
        UpdateOperationalState(barberId, BarberState.Offline);
    }

    public BarberPanelStartResult StartService(string stationInput, string scannedTicketNumber)
    {
        var stationNumber = ParseStationNumber(stationInput);
        if (string.IsNullOrWhiteSpace(scannedTicketNumber))
        {
            throw new InvalidOperationException("Scan or enter an assigned ticket.");
        }

        var now = OperationalClock.Now;
        BarberPanelStartResult? result = null;

        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            var deviceId = Environment.MachineName;
            DailyOperationCoordinator.EnsureDailyReset(connection, sqliteTransaction, now, deviceId);

            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var turnRepository = new LocalTurnRepository(connection, sqliteTransaction);
            var appointmentRepository = new AppointmentReservationRepository(connection, sqliteTransaction);
            var dailyRotationRepository = new DailyRotationRepository(connection, sqliteTransaction);
            var auditRepository = new AuditEventRepository(connection, sqliteTransaction);
            var syncRecorder = new SyncOutboxRecorder(new SyncOutboxRepository(connection, sqliteTransaction));

            var scannedBarber = barberRepository.GetActiveByStationNumber(stationNumber)
                ?? throw new InvalidOperationException("Station does not belong to an active barber.");

            var turn = turnRepository.GetByTicketInputForToday(scannedTicketNumber, now);
            if (turn is null && IsAppointmentCode(scannedTicketNumber))
            {
                result = StartAppointmentService(
                    scannedTicketNumber,
                    scannedBarber,
                    now,
                    barberRepository,
                    turnRepository,
                    appointmentRepository,
                    syncRecorder);
                return;
            }

            if (turn is null)
            {
                throw new InvalidOperationException("Ticket does not exist");
            }

            if (turn.State == TurnState.InService)
            {
                throw new InvalidOperationException("This ticket is already being attended by another barber.");
            }

            if (turn.State == TurnState.Completed)
            {
                throw new InvalidOperationException($"This ticket was already completed and charged by {scannedBarber.DisplayName}.");
            }

            if (turn.State is not (TurnState.Waiting or TurnState.Called))
            {
                throw new InvalidOperationException("The ticket is not ready to start service.");
            }

            if (turn.AssignedBarberId == scannedBarber.Id && turn.State == TurnState.Called)
            {
                if (scannedBarber.State != BarberState.Called)
                {
                    throw new InvalidOperationException("The barber must have a called ticket before starting service.");
                }

                result = StartCalledTicket(
                    turn,
                    scannedBarber,
                    now,
                    turnRepository,
                    barberRepository,
                    syncRecorder,
                    enqueueCalledEvent: false,
                    deviceId);
                return;
            }

            result = TransferScannedTicket(
                turn,
                scannedBarber,
                now,
                turnRepository,
                barberRepository,
                dailyRotationRepository,
                appointmentRepository,
                auditRepository,
                syncRecorder,
                deviceId);
        });

        return result ?? throw new InvalidOperationException("Service could not be started.");
    }

    private BarberPanelStartResult TransferScannedTicket(
        Turn turn,
        Barber targetBarber,
        DateTimeOffset now,
        LocalTurnRepository turnRepository,
        LocalBarberRepository barberRepository,
        DailyRotationRepository dailyRotationRepository,
        AppointmentReservationRepository appointmentRepository,
        AuditEventRepository auditRepository,
        SyncOutboxRecorder syncRecorder,
        string deviceId)
    {
        if (!HasAssignedOrReservedBarber(turn))
        {
            throw new InvalidOperationException("The ticket is not assigned or reserved for a barber.");
        }

        if (targetBarber.State == BarberState.Available)
        {
            turnRepository.AssignManuallyToBarber(turn.Id, targetBarber.Id, now);
            barberRepository.SetState(targetBarber.Id, BarberState.Called, now);
            var previousBarberReleased = ReleasePreviousCalledBarber(turn, targetBarber.Id, barberRepository, now);
            var previousBarber = GetPreviousBarber(turn, targetBarber.Id, barberRepository);

            var result = StartCalledTicket(
                turn,
                targetBarber,
                now,
                turnRepository,
                barberRepository,
                syncRecorder,
                enqueueCalledEvent: true,
                deviceId);

            if (previousBarberReleased)
            {
                AssignNextWaitingTurnIfPossible(
                    turnRepository,
                    barberRepository,
                    dailyRotationRepository,
                    appointmentRepository,
                    auditRepository,
                    syncRecorder,
                    now,
                    deviceId,
                    "barber_panel_auto_transfer_started");
            }

            auditRepository.Add(new AuditEvent(
                Guid.NewGuid(),
                now,
                "barber_panel_ticket_transferred_started",
                "turn",
                turn.Id,
                JsonSerializer.Serialize(new
                {
                    turnId = turn.Id,
                    displayTicketNumber = turn.DisplayTicketNumber,
                    internalTicketNumber = turn.TicketNumber,
                    previousBarberId = turn.AssignedBarberId,
                    targetBarberId = targetBarber.Id,
                    targetBarberName = targetBarber.DisplayName,
                    resultTurnState = TurnState.InService.ToString()
                }),
                deviceId));
            EnqueueAutoReassignmentRecord(
                syncRecorder,
                turn,
                now,
                targetBarber,
                previousBarber,
                "started",
                TurnState.InService,
                previousBarberReleased,
                deviceId);

            return result;
        }

        if (targetBarber.State is BarberState.Called or BarberState.InService)
        {
            turnRepository.ReserveForBarber(turn.Id, targetBarber.Id, now);
            var previousBarberReleased = ReleasePreviousCalledBarber(turn, targetBarber.Id, barberRepository, now);
            var previousBarber = GetPreviousBarber(turn, targetBarber.Id, barberRepository);

            var reservedTurn = turnRepository.GetById(turn.Id)
                ?? throw new InvalidOperationException("Ticket was not found after reassignment.");
            syncRecorder.Enqueue(new LocalSyncEvent(
                Guid.NewGuid(),
                now,
                "ticket.called",
                "ticket",
                turn.Id,
                JsonSerializer.Serialize(TicketSyncPayload.Create(reservedTurn, "waiting", targetBarber.Id)),
                deviceId), now);

            if (previousBarberReleased)
            {
                AssignNextWaitingTurnIfPossible(
                    turnRepository,
                    barberRepository,
                    dailyRotationRepository,
                    appointmentRepository,
                    auditRepository,
                    syncRecorder,
                    now,
                    deviceId,
                    "barber_panel_auto_transfer_reserved");
            }

            auditRepository.Add(new AuditEvent(
                Guid.NewGuid(),
                now,
                "barber_panel_ticket_transferred_waiting",
                "turn",
                turn.Id,
                JsonSerializer.Serialize(new
                {
                    turnId = turn.Id,
                    displayTicketNumber = turn.DisplayTicketNumber,
                    internalTicketNumber = turn.TicketNumber,
                    previousBarberId = turn.AssignedBarberId,
                    targetBarberId = targetBarber.Id,
                    targetBarberName = targetBarber.DisplayName,
                    targetPreviousState = targetBarber.State.ToString(),
                    resultTurnState = TurnState.Waiting.ToString()
                }),
                deviceId));
            EnqueueAutoReassignmentRecord(
                syncRecorder,
                reservedTurn,
                now,
                targetBarber,
                previousBarber,
                "waiting",
                TurnState.Waiting,
                previousBarberReleased,
                deviceId);

            return new BarberPanelStartResult(
                targetBarber.Id,
                reservedTurn.DisplayTicketNumber,
                reservedTurn.TicketNumber,
                targetBarber.DisplayName,
                targetBarber.StationCode ?? throw new InvalidOperationException("The active barber does not have an assigned station."),
                now,
                "Ticket moved to waiting for the scanned barber.",
                BarberPanelStartOutcome.ReassignedToWaiting);
        }

        throw new InvalidOperationException("The barber must be available, called, or in service to receive this ticket.");
    }

    private static BarberPanelStartResult StartCalledTicket(
        Turn turn,
        Barber barber,
        DateTimeOffset now,
        LocalTurnRepository turnRepository,
        LocalBarberRepository barberRepository,
        SyncOutboxRecorder syncRecorder,
        bool enqueueCalledEvent,
        string deviceId)
    {
        if (enqueueCalledEvent)
        {
            var calledTurn = turnRepository.GetById(turn.Id)
                ?? throw new InvalidOperationException("Ticket was not found after reassignment.");
            syncRecorder.Enqueue(new LocalSyncEvent(
                Guid.NewGuid(),
                now,
                "ticket.called",
                "ticket",
                turn.Id,
                JsonSerializer.Serialize(TicketSyncPayload.Create(calledTurn, "called", barber.Id)),
                deviceId), now);
        }

        turnRepository.MarkInService(turn.Id, barber.Id, now);
        barberRepository.SetState(barber.Id, BarberState.InService, now);

        var startedTurn = turnRepository.GetById(turn.Id)
            ?? throw new InvalidOperationException("Ticket was not found after service start.");
        syncRecorder.Enqueue(new LocalSyncEvent(
            Guid.NewGuid(),
            now,
            "ticket.started",
            "ticket",
            turn.Id,
            JsonSerializer.Serialize(TicketSyncPayload.Create(startedTurn, "in_progress", barber.Id, now)),
            deviceId), now);

        return new BarberPanelStartResult(
            barber.Id,
            startedTurn.DisplayTicketNumber,
            startedTurn.TicketNumber,
            barber.DisplayName,
            barber.StationCode ?? throw new InvalidOperationException("The active barber does not have an assigned station."),
            now,
            "Service started locally. Payment and closeout remain in Cash Box.");
    }

    private void AssignNextWaitingTurnIfPossible(
        LocalTurnRepository turnRepository,
        LocalBarberRepository barberRepository,
        DailyRotationRepository dailyRotationRepository,
        AppointmentReservationRepository appointmentRepository,
        AuditEventRepository auditRepository,
        SyncOutboxRecorder syncRecorder,
        DateTimeOffset now,
        string deviceId,
        string reason)
    {
        var reassignment = TryAssignNextWaitingTurn(
            turnRepository,
            barberRepository,
            dailyRotationRepository,
            appointmentRepository,
            now);

        if (reassignment is null)
        {
            return;
        }

        var reassignedTurn = turnRepository.GetById(reassignment.TurnId);
        if (reassignedTurn is not null)
        {
            syncRecorder.Enqueue(new LocalSyncEvent(
                Guid.NewGuid(),
                now,
                "ticket.called",
                "ticket",
                reassignment.TurnId,
                JsonSerializer.Serialize(TicketSyncPayload.Create(reassignedTurn, "called", reassignment.BarberId)),
                deviceId), now);
        }

        auditRepository.Add(new AuditEvent(
            Guid.NewGuid(),
            now,
            "barber_panel_waiting_turn_assigned",
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
                reason
            }),
            deviceId));
    }

    private TurnAssignmentDecision? TryAssignNextWaitingTurn(
        LocalTurnRepository turnRepository,
        LocalBarberRepository barberRepository,
        DailyRotationRepository dailyRotationRepository,
        AppointmentReservationRepository appointmentRepository,
        DateTimeOffset now)
    {
        var allBarbers = barberRepository
            .ListAll()
            .Where(barber => barber.IsActive)
            .ToArray();
        var waitingTurns = turnRepository.ListWaiting();
        var businessDate = DailyOperationCoordinator.GetBusinessDate(now);
        var dailyRotationEntries = dailyRotationRepository.ListByDate(businessDate);
        var barbers = DailyRotationQueue.CheckedInBarbers(allBarbers, dailyRotationEntries, businessDate)
            .ToArray();
        var rotationQueue = DailyRotationQueue.Build(
            barbers,
            dailyRotationEntries,
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

    private static bool ReleasePreviousCalledBarber(
        Turn turn,
        Guid targetBarberId,
        LocalBarberRepository barberRepository,
        DateTimeOffset now)
    {
        if (turn.AssignedBarberId is not Guid previousBarberId || previousBarberId == targetBarberId)
        {
            return false;
        }

        var previousBarber = barberRepository.GetById(previousBarberId)
            ?? throw new InvalidOperationException("Assigned barber was not found in the local database.");
        if (previousBarber.IsActive && previousBarber.State == BarberState.Called)
        {
            barberRepository.SetState(previousBarber.Id, BarberState.Available, now);
            return true;
        }

        return false;
    }

    private static Barber? GetPreviousBarber(Turn turn, Guid targetBarberId, LocalBarberRepository barberRepository)
    {
        Guid? previousId = turn.AssignedBarberId;
        
        if (previousId is null && turn.RequestedBarberIds?.Count > 0)
        {
            previousId = turn.RequestedBarberIds.First();
        }

        if (previousId is not Guid previousBarberId || previousBarberId == targetBarberId)
        {
            return null;
        }

        return barberRepository.GetById(previousBarberId);
    }

    private static void EnqueueAutoReassignmentRecord(
        SyncOutboxRecorder syncRecorder,
        Turn turn,
        DateTimeOffset now,
        Barber targetBarber,
        Barber? previousBarber,
        string outcome,
        TurnState resultTurnState,
        bool previousBarberReleased,
        string deviceId)
    {
        syncRecorder.Enqueue(new LocalSyncEvent(
            Guid.NewGuid(),
            now,
            "ticket.auto_reassigned",
            "ticket",
            turn.Id,
            JsonSerializer.Serialize(new
            {
                ticket_id = turn.Id,
                display_ticket_number = turn.DisplayTicketNumber,
                internal_ticket_number = turn.TicketNumber,
                previous_barber_id = previousBarber?.Id,
                previous_barber_name = previousBarber?.DisplayName,
                previous_station_code = previousBarber?.StationCode,
                target_barber_id = targetBarber.Id,
                target_barber_name = targetBarber.DisplayName,
                target_station_code = targetBarber.StationCode,
                outcome,
                result_turn_state = resultTurnState.ToString(),
                previous_barber_released = previousBarberReleased,
                occurred_at = now
            }),
            deviceId), now);
    }

    private static bool HasAssignedOrReservedBarber(Turn turn)
    {
        return turn.AssignedBarberId is not null || turn.RequestedBarberIds?.Count > 0;
    }

    private static BarberPanelStartResult StartAppointmentService(
        string scannedAppointmentCode,
        Barber scannedBarber,
        DateTimeOffset now,
        LocalBarberRepository barberRepository,
        LocalTurnRepository turnRepository,
        AppointmentReservationRepository appointmentRepository,
        SyncOutboxRecorder syncRecorder)
    {
        var appointment = appointmentRepository.GetByAppointmentCode(scannedAppointmentCode)
            ?? throw new InvalidOperationException("Appointment QR does not exist in the local database.");

        if (appointment.AppointmentCode is null)
        {
            throw new InvalidOperationException("Appointment has no QR code.");
        }

        if (appointment.State is AppointmentState.Cancelled or AppointmentState.NoShow or AppointmentState.Completed)
        {
            throw new InvalidOperationException("This appointment is no longer valid for check-in.");
        }

        if (turnRepository.GetByAppointmentId(appointment.Id) is not null)
        {
            throw new InvalidOperationException("This appointment already has a local service ticket.");
        }

        var validFrom = appointment.ScheduledFor.Subtract(appointment.ProtectionWindow);
        var validUntil = appointment.ScheduledFor.AddMinutes(10);
        if (now < validFrom || now > validUntil)
        {
            throw new InvalidOperationException("Appointment QR can only be scanned from 15 minutes before to 10 minutes after the appointment time.");
        }

        if (appointment.BarberId != scannedBarber.Id)
        {
            throw new InvalidOperationException("Station does not match the appointment barber.");
        }

        var barber = scannedBarber;
        if (barber.State != BarberState.Available)
        {
            throw new InvalidOperationException("The appointment barber must be available before starting this appointment.");
        }

        var ticketDate = OperationalClock.GetBusinessDate(now);
        var turn = new Turn(
            Guid.NewGuid(),
            appointment.AppointmentCode,
            turnRepository.GetNextDisplayTicketNumber(ticketDate),
            ticketDate,
            TurnState.InService,
            TurnSource.Appointment,
            now,
            appointment.BarberId,
            appointment.Id,
            [appointment.BarberId],
            appointment.CustomerName,
            startedAt: now);

        turnRepository.Upsert(turn, now);
        barberRepository.SetState(appointment.BarberId, BarberState.InService, now);
        appointmentRepository.MarkCheckedIn(appointment.Id, now, now);

        syncRecorder.Enqueue(new LocalSyncEvent(
            Guid.NewGuid(),
            now,
            "appointment.checked_in",
            "appointment",
            appointment.Id,
            JsonSerializer.Serialize(new
            {
                appointment_id = appointment.Id,
                appointment_code = appointment.AppointmentCode,
                ticket_id = turn.Id,
                barber_id = appointment.BarberId,
                checked_in_at = now
            }),
            Environment.MachineName), now);

        syncRecorder.Enqueue(new LocalSyncEvent(
            Guid.NewGuid(),
            now,
            "ticket.started",
            "ticket",
            turn.Id,
            JsonSerializer.Serialize(TicketSyncPayload.Create(turn, "in_progress", appointment.BarberId, now)),
            Environment.MachineName), now);

        return new BarberPanelStartResult(
            barber.Id,
            turn.DisplayTicketNumber,
            turn.TicketNumber,
            barber.DisplayName,
            barber.StationCode ?? throw new InvalidOperationException("The active barber does not have an assigned station."),
            now,
            "Appointment service started locally. Payment and closeout remain in Cash Box.");
    }

    private static int ParseStationNumber(string stationInput)
    {
        if (string.IsNullOrWhiteSpace(stationInput))
        {
            throw new InvalidOperationException("Scan or enter the barber station.");
        }

        var normalized = stationInput.Trim();
        if (normalized.StartsWith("B", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..].Trim();
        }

        if (normalized.StartsWith("-", StringComparison.Ordinal))
        {
            normalized = normalized[1..].Trim();
        }

        if (!int.TryParse(normalized, out var stationNumber) || stationNumber <= 0)
        {
            throw new InvalidOperationException("Station code must use format B-#.");
        }

        return stationNumber;
    }

    private static bool IsAppointmentCode(string value)
    {
        var normalized = value.Trim();
        return normalized.Length == 13
            && normalized.StartsWith("A", StringComparison.OrdinalIgnoreCase)
            && normalized.Skip(1).All(Uri.IsHexDigit);
    }

    private void UpdateOperationalState(Guid barberId, BarberState state)
    {
        var now = OperationalClock.Now;
        var deviceId = Environment.MachineName;
        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            DailyOperationCoordinator.EnsureDailyReset(connection, sqliteTransaction, now, deviceId);

            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var barber = barberRepository.GetById(barberId)
                ?? throw new InvalidOperationException("Barber was not found in the local database.");
            if (!barber.IsActive)
            {
                throw new InvalidOperationException("This barber is disabled by administration.");
            }

            if (barber.State is BarberState.Called or BarberState.InService)
            {
                throw new InvalidOperationException("Availability cannot be changed while a ticket is called or in service.");
            }

            barberRepository.SetState(barberId, state, now);
        });
    }
}
