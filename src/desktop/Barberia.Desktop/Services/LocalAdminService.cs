using System.Text.Json;
using Barberia.Core.Assignment;
using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Barberia.Data.Reports;
using Microsoft.Data.Sqlite;

namespace Barberia.Desktop.Services;

public sealed class LocalAdminService
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly TurnAssignmentEngine _assignmentEngine = new();

    public LocalAdminService()
        : this(LocalDesktopDatabase.CreateConnectionFactory())
    {
    }

    public LocalAdminService(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        new LocalDatabaseInitializer(_connectionFactory).Initialize();
    }

    public LocalAdminSnapshot Load()
    {
        var now = DateTimeOffset.Now;
        var from = new DateTimeOffset(now.Date, now.Offset);
        var to = from.AddDays(1);

        using var connection = _connectionFactory.OpenConnection();
        var report = new LocalAdminReportRepository(connection).Load(from, to, now);
        var barbers = new LocalBarberRepository(connection).ListAll();
        var activeTurns = new LocalTurnRepository(connection).ListActiveForPublicDisplay();
        var auditEvents = new AuditEventRepository(connection)
            .ListAll()
            .OrderByDescending(auditEvent => auditEvent.OccurredAt)
            .Take(10)
            .ToArray();
        var databaseSize = File.Exists(LocalAppPaths.DatabasePath)
            ? new FileInfo(LocalAppPaths.DatabasePath).Length
            : 0;

        return new LocalAdminSnapshot(
            now,
            LocalAppPaths.DatabasePath,
            databaseSize,
            report.Operations,
            report.Cash,
            barbers,
            ProfileImageCatalog.ListProfileImages(),
            activeTurns,
            auditEvents);
    }

    public void SaveBarber(
        Guid? barberId,
        string displayName,
        int rotationOrder,
        int? stationNumber,
        string? profileImagePath,
        bool isActive)
    {
        var normalizedName = NormalizeDisplayName(displayName);
        var normalizedStationNumber = NormalizeStationNumber(stationNumber, isActive);
        var normalizedProfileImagePath = NormalizeProfileImagePath(profileImagePath);
        if (rotationOrder < 0)
        {
            throw new InvalidOperationException("Rotation order must be zero or greater.");
        }

        var now = DateTimeOffset.Now;
        var deviceId = Environment.MachineName;
        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var auditRepository = new AuditEventRepository(connection, sqliteTransaction);
            var existing = barberId is null ? null : barberRepository.GetById(barberId.Value);

            if (barberId is not null && existing is null)
            {
                throw new InvalidOperationException("Barber was not found in the local database.");
            }

            var existingState = existing?.State ?? BarberState.Available;
            if (!isActive && existingState is (BarberState.Called or BarberState.InService))
            {
                throw new InvalidOperationException("A called or in-service barber cannot be deactivated.");
            }

            if (normalizedStationNumber is int activeStationNumber)
            {
                EnsureStationAvailable(barberRepository, barberId, activeStationNumber);
            }

            var barber = new Barber(
                barberId ?? Guid.NewGuid(),
                normalizedName,
                isActive ? existingState : BarberState.Offline,
                existing?.ClientsServedToday ?? 0,
                rotationOrder,
                existing?.CheckedInAt,
                normalizedStationNumber,
                normalizedProfileImagePath,
                isActive);

            barberRepository.Upsert(barber, now);
            auditRepository.Add(new AuditEvent(
                Guid.NewGuid(),
                now,
                barberId is null ? "admin_barber_created" : "admin_barber_updated",
                "barber",
                barber.Id,
                JsonSerializer.Serialize(new
                {
                    barberId = barber.Id,
                    barberName = barber.DisplayName,
                    barber.RotationOrder,
                    previousStationCode = existing?.StationCode,
                    barber.StationCode,
                    barber.ProfileImagePath,
                    barber.IsActive
                }),
                deviceId));
        });
    }

    public IReadOnlyList<ProfileImageOption> LoadProfileImages()
    {
        return ProfileImageCatalog.ListProfileImages();
    }

    public string ImportProfileImage(string sourcePath)
    {
        return ProfileImageCatalog.ImportProfileImage(sourcePath);
    }

    public void DeleteBarber(Guid barberId)
    {
        if (barberId == Guid.Empty)
        {
            throw new InvalidOperationException("Select a barber before deleting.");
        }

        var now = DateTimeOffset.Now;
        var deviceId = Environment.MachineName;
        var transaction = new LocalDataTransaction(_connectionFactory);

        try
        {
            transaction.Execute((connection, sqliteTransaction) =>
            {
                var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
                var auditRepository = new AuditEventRepository(connection, sqliteTransaction);
                var barber = barberRepository.GetById(barberId)
                    ?? throw new InvalidOperationException("Barber was not found in the local database.");

                if (barber.State is BarberState.Called or BarberState.InService)
                {
                    throw new InvalidOperationException("A called or in-service barber cannot be deleted.");
                }

                barberRepository.Delete(barberId);
                auditRepository.Add(new AuditEvent(
                    Guid.NewGuid(),
                    now,
                    "admin_barber_deleted",
                    "barber",
                    barberId,
                    JsonSerializer.Serialize(new
                    {
                        barberId,
                        barberName = barber.DisplayName
                    }),
                    deviceId));
            });
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException(
                "This barber already has local history and cannot be deleted. Set them Offline instead.",
                exception);
        }
    }

    public void MarkBarberAvailable(Guid barberId)
    {
        UpdateBarberState(barberId, BarberState.Available);
    }

    public void MarkBarberOffline(Guid barberId)
    {
        UpdateBarberState(barberId, BarberState.Offline);
    }

    public void ActivateBarber(Guid barberId, int stationNumber)
    {
        UpdateBarberActiveState(barberId, isActive: true, stationNumber);
    }

    public void DeactivateBarber(Guid barberId)
    {
        UpdateBarberActiveState(barberId, isActive: false, stationNumber: null);
    }

    public void CancelTurn(Guid turnId)
    {
        if (turnId == Guid.Empty)
        {
            throw new InvalidOperationException("Select a ticket before cancelling.");
        }

        var now = DateTimeOffset.Now;
        var deviceId = Environment.MachineName;
        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            var turnRepository = new LocalTurnRepository(connection, sqliteTransaction);
            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var auditRepository = new AuditEventRepository(connection, sqliteTransaction);
            var turn = turnRepository.GetById(turnId)
                ?? throw new InvalidOperationException("Ticket was not found in the local database.");

            if (turn.State is not (TurnState.Waiting or TurnState.Assigned or TurnState.Called or TurnState.InService))
            {
                throw new InvalidOperationException("Only active tickets can be cancelled from Local Admin.");
            }

            Barber? assignedBarber = null;
            if (turn.AssignedBarberId is Guid barberId)
            {
                assignedBarber = barberRepository.GetById(barberId)
                    ?? throw new InvalidOperationException("Assigned barber was not found in the local database.");
            }

            turnRepository.MarkCancelled(turnId, now);

            if (assignedBarber is not null && assignedBarber.IsActive)
            {
                barberRepository.SetState(assignedBarber.Id, BarberState.Available, now);
            }

            var reassignment = TryAssignNextWaitingTurn(
                turnRepository,
                barberRepository,
                new AppointmentReservationRepository(connection, sqliteTransaction),
                now);

            auditRepository.Add(new AuditEvent(
                Guid.NewGuid(),
                now,
                "admin_turn_cancelled",
                "turn",
                turnId,
                JsonSerializer.Serialize(new
                {
                    turnId,
                    displayTicketNumber = turn.DisplayTicketNumber,
                    internalTicketNumber = turn.TicketNumber,
                    customerName = turn.CustomerName,
                    previousState = turn.State.ToString(),
                    assignedBarberId = assignedBarber?.Id,
                    assignedBarberName = assignedBarber?.DisplayName,
                    barberReleased = assignedBarber?.IsActive == true,
                    reassignedDisplayTicketNumber = reassignment?.DisplayTicketNumber,
                    reassignedInternalTicketNumber = reassignment?.TicketNumber,
                    reassignedBarberId = reassignment?.BarberId
                }),
                deviceId));

            if (reassignment is not null)
            {
                auditRepository.Add(new AuditEvent(
                    Guid.NewGuid(),
                    now,
                    "admin_waiting_turn_assigned",
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
                        reason = "ticket_cancelled"
                    }),
                    deviceId));
            }
        });
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

    private void UpdateBarberState(Guid barberId, BarberState state)
    {
        if (barberId == Guid.Empty)
        {
            throw new InvalidOperationException("Select a barber before changing status.");
        }

        var now = DateTimeOffset.Now;
        var deviceId = Environment.MachineName;
        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var auditRepository = new AuditEventRepository(connection, sqliteTransaction);
            var barber = barberRepository.GetById(barberId)
                ?? throw new InvalidOperationException("Barber was not found in the local database.");

            if (!barber.IsActive)
            {
                throw new InvalidOperationException("Activate the barber before changing availability.");
            }

            if (barber.State is BarberState.Called or BarberState.InService)
            {
                throw new InvalidOperationException("A called or in-service barber cannot be changed from Local Admin.");
            }

            barberRepository.SetState(barberId, state, now);
            auditRepository.Add(new AuditEvent(
                Guid.NewGuid(),
                now,
                "admin_barber_state_changed",
                "barber",
                barberId,
                JsonSerializer.Serialize(new
                {
                    barberId,
                    barberName = barber.DisplayName,
                    previousState = barber.State.ToString(),
                    state = state.ToString()
                }),
                deviceId));
        });
    }

    private void UpdateBarberActiveState(Guid barberId, bool isActive, int? stationNumber)
    {
        if (barberId == Guid.Empty)
        {
            throw new InvalidOperationException("Select a barber before changing active status.");
        }

        var now = DateTimeOffset.Now;
        var deviceId = Environment.MachineName;
        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var auditRepository = new AuditEventRepository(connection, sqliteTransaction);
            var barber = barberRepository.GetById(barberId)
                ?? throw new InvalidOperationException("Barber was not found in the local database.");

            if (!isActive && barber.State is (BarberState.Called or BarberState.InService))
            {
                throw new InvalidOperationException("A called or in-service barber cannot be deactivated.");
            }

            var normalizedStationNumber = NormalizeStationNumber(stationNumber, isActive);
            if (normalizedStationNumber is int activeStationNumber)
            {
                EnsureStationAvailable(barberRepository, barberId, activeStationNumber);
            }

            barberRepository.SetActive(barberId, isActive, now, normalizedStationNumber);
            if (!isActive && barber.State != BarberState.Offline)
            {
                barberRepository.SetState(barberId, BarberState.Offline, now);
            }

            auditRepository.Add(new AuditEvent(
                Guid.NewGuid(),
                now,
                "admin_barber_active_changed",
                "barber",
                barberId,
                JsonSerializer.Serialize(new
                {
                    barberId,
                    barberName = barber.DisplayName,
                    previousIsActive = barber.IsActive,
                    previousStationCode = barber.StationCode,
                    stationCode = normalizedStationNumber is null ? null : FormatStationCode(normalizedStationNumber.Value),
                    isActive
                }),
                deviceId));
        });
    }

    private static string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new InvalidOperationException("Barber name is required.");
        }

        return displayName.Trim();
    }

    private static string? NormalizeProfileImagePath(string? profileImagePath)
    {
        if (string.IsNullOrWhiteSpace(profileImagePath))
        {
            return null;
        }

        var normalized = profileImagePath.Trim().Replace('\\', '/');
        if (ProfileImageCatalog.ResolveImageUri(normalized) is null)
        {
            throw new InvalidOperationException("Select an image from the list or import one from Windows Explorer.");
        }

        return normalized;
    }

    private static int? NormalizeStationNumber(int? stationNumber, bool isActive)
    {
        if (!isActive)
        {
            return null;
        }

        if (stationNumber is null)
        {
            throw new InvalidOperationException("Station code is required for active barbers.");
        }

        if (stationNumber <= 0)
        {
            throw new InvalidOperationException("Station number must be positive.");
        }

        return stationNumber;
    }

    private static void EnsureStationAvailable(
        LocalBarberRepository barberRepository,
        Guid? currentBarberId,
        int stationNumber)
    {
        var duplicate = barberRepository
            .ListAll()
            .FirstOrDefault(barber =>
                barber.IsActive
                && barber.StationNumber == stationNumber
                && barber.Id != currentBarberId);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"{FormatStationCode(stationNumber)} is already assigned to {duplicate.DisplayName}.");
        }
    }

    private static string FormatStationCode(int stationNumber)
    {
        return $"B-{stationNumber}";
    }
}
