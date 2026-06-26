using System.Text.Json;
using Barberia.Core.Assignment;
using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Barberia.Data.Sync;
using Barberia.Sync.Outbox;

namespace Barberia.Desktop.Services;

public sealed class BarberCheckInService
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly TurnAssignmentEngine _assignmentEngine = new();

    public BarberCheckInService()
        : this(LocalDesktopDatabase.CreateConnectionFactory())
    {
    }

    public BarberCheckInService(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        new LocalDatabaseInitializer(_connectionFactory).Initialize();
    }

    public BarberCheckInSnapshot Load()
    {
        var now = OperationalClock.Now;
        var deviceId = Environment.MachineName;
        DailyOperationCoordinator.EnsureDailyReset(_connectionFactory, now, deviceId);
        var businessDate = DailyOperationCoordinator.GetBusinessDate(now);

        using var connection = _connectionFactory.OpenConnection();
        var dailyRotationEntries = new DailyRotationRepository(connection).ListByDate(businessDate);
        var barbers = DailyRotationQueue.OrderBarbers(
            new LocalBarberRepository(connection)
                .ListAll()
                .Where(barber => barber.IsActive),
            dailyRotationEntries,
            businessDate);

        return new BarberCheckInSnapshot(now, barbers, dailyRotationEntries);
    }

    public BarberCheckInResult CheckIn(string stationInput)
    {
        var stationNumber = ParseStationNumber(stationInput);
        var now = OperationalClock.Now;
        var deviceId = Environment.MachineName;
        BarberCheckInResult? result = null;

        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            DailyOperationCoordinator.EnsureDailyReset(connection, sqliteTransaction, now, deviceId);

            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var turnRepository = new LocalTurnRepository(connection, sqliteTransaction);
            var dailyRotationRepository = new DailyRotationRepository(connection, sqliteTransaction);
            var appointmentRepository = new AppointmentReservationRepository(connection, sqliteTransaction);
            var auditRepository = new AuditEventRepository(connection, sqliteTransaction);
            var syncRecorder = new SyncOutboxRecorder(new SyncOutboxRepository(connection, sqliteTransaction));
            var businessDate = DailyOperationCoordinator.GetBusinessDate(now);

            var barber = barberRepository.GetActiveByStationNumber(stationNumber)
                ?? throw new InvalidOperationException($"No active barber is assigned to B-{stationNumber}.");

            if (barber.State is BarberState.Called or BarberState.InService)
            {
                throw new InvalidOperationException("This barber already has a called ticket or service in progress.");
            }

            var existingEntry = dailyRotationRepository.Get(businessDate, barber.Id);
            var arrivedAt = existingEntry?.ArrivedAt ?? now;
            var updatedAt = now > barber.UpdatedAt ? now : barber.UpdatedAt.AddSeconds(1);

            barberRepository.SetStateAndCheckedInAt(barber.Id, BarberState.Available, arrivedAt, updatedAt);
            dailyRotationRepository.EnsureQueued(businessDate, barber.Id, arrivedAt, updatedAt);

            var updatedEntries = dailyRotationRepository.ListByDate(businessDate);
            var rotationEntry = updatedEntries.First(entry => entry.BarberId == barber.Id);
            var reassignment = TryAssignNextWaitingTurn(
                turnRepository,
                barberRepository,
                dailyRotationRepository,
                appointmentRepository,
                now);

            if (reassignment is not null)
            {
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
                    "barber_checkin_waiting_turn_assigned",
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
                        reason = "barber_station_checked_in"
                    }),
                    deviceId));
            }

            auditRepository.Add(new AuditEvent(
                Guid.NewGuid(),
                now,
                "barber_station_checked_in",
                "barber",
                barber.Id,
                JsonSerializer.Serialize(new
                {
                    barberId = barber.Id,
                    barberName = barber.DisplayName,
                    stationCode = barber.StationCode,
                    queuePosition = rotationEntry.QueuePosition,
                    arrivedAt,
                    existingCheckIn = existingEntry is not null,
                    assignedDisplayTicketNumber = reassignment?.DisplayTicketNumber,
                    assignedInternalTicketNumber = reassignment?.TicketNumber
                }),
                deviceId));

            result = new BarberCheckInResult(
                barber.Id,
                barber.DisplayName,
                barber.StationCode ?? $"B-{stationNumber}",
                rotationEntry.QueuePosition + 1,
                arrivedAt,
                reassignment?.DisplayTicketNumber);
        });

        return result ?? throw new InvalidOperationException("Barber check-in could not be completed.");
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
        var rotationQueue = DailyRotationQueue.Build(barbers, dailyRotationEntries, businessDate);
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
}

public sealed record BarberCheckInSnapshot(
    DateTimeOffset LoadedAt,
    IReadOnlyList<Barber> Barbers,
    IReadOnlyList<DailyRotationEntry> DailyRotationEntries);

public sealed record BarberCheckInResult(
    Guid BarberId,
    string BarberName,
    string BarberStationCode,
    int QueuePosition,
    DateTimeOffset ArrivedAt,
    int? AssignedDisplayTicketNumber);