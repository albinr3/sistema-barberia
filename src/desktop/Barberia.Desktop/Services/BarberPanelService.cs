using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Repositories;
using Barberia.Data.Sync;
using Barberia.Sync.Outbox;
using System.Text.Json;

namespace Barberia.Desktop.Services;

public sealed class BarberPanelService
{
    private readonly SqliteConnectionFactory _connectionFactory;

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
            DailyOperationCoordinator.EnsureDailyReset(connection, sqliteTransaction, now, Environment.MachineName);

            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var turnRepository = new LocalTurnRepository(connection, sqliteTransaction);
            var appointmentRepository = new AppointmentReservationRepository(connection, sqliteTransaction);

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
                    new SyncOutboxRecorder(new SyncOutboxRepository(connection, sqliteTransaction)));
                return;
            }

            if (turn is null)
            {
                throw new InvalidOperationException("Ticket does not exist");
            }

            var barberId = turn.AssignedBarberId
                ?? throw new InvalidOperationException("The ticket does not have an assigned barber.");
            if (barberId != scannedBarber.Id)
            {
                throw new InvalidOperationException("Station does not match the assigned ticket barber.");
            }

            var barber = scannedBarber;

            if (turn.State == TurnState.InService)
            {
                throw new InvalidOperationException("This ticket is already being attended by another barber.");
            }

            if (turn.State == TurnState.Completed)
            {
                throw new InvalidOperationException($"This ticket was already completed and charged by {barber.DisplayName}.");
            }

            if (!barber.IsActive)
            {
                throw new InvalidOperationException("The assigned barber is disabled by administration.");
            }

            if (turn.State is not TurnState.Called)
            {
                throw new InvalidOperationException("The ticket is not ready to start service.");
            }

            if (barber.State != BarberState.Called)
            {
                throw new InvalidOperationException("The barber must have a called ticket before starting service.");
            }

            turnRepository.MarkInService(turn.Id, barberId, now);
            barberRepository.SetState(barberId, BarberState.InService, now);

            var syncRecorder = new SyncOutboxRecorder(new SyncOutboxRepository(connection, sqliteTransaction));
            syncRecorder.Enqueue(new LocalSyncEvent(
                Guid.NewGuid(), now, "ticket.started", "ticket", turn.Id,
                JsonSerializer.Serialize(TicketSyncPayload.Create(turn, "in_progress", barberId, now)),
                Environment.MachineName), now);

            result = new BarberPanelStartResult(
                barber.Id,
                turn.DisplayTicketNumber,
                turn.TicketNumber,
                barber.DisplayName,
                barber.StationCode ?? throw new InvalidOperationException("The active barber does not have an assigned station."),
                now,
                "Service started locally. Payment and closeout remain in Cash Box.");
        });

        return result ?? throw new InvalidOperationException("Service could not be started.");
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

            if (state == BarberState.Available)
            {
                var businessDate = DailyOperationCoordinator.GetBusinessDate(now);
                var checkedInAt = barber.CheckedInAt is DateTimeOffset existingCheckedInAt
                    && OperationalClock.GetBusinessDate(existingCheckedInAt) == businessDate
                        ? existingCheckedInAt
                        : now;

                barberRepository.SetStateAndCheckedInAt(barberId, state, checkedInAt, now);
                new DailyRotationRepository(connection, sqliteTransaction)
                    .EnsureQueued(businessDate, barberId, checkedInAt, now);
            }
            else
            {
                barberRepository.SetState(barberId, state, now);
            }
        });
    }
}
