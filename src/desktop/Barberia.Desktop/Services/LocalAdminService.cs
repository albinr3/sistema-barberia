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
        var services = new ServiceRepository(connection).ListAll();
        var activeTurnRows = new LocalTurnRepository(connection).ListActiveWithUpdatedAt();
        var activeTurns = activeTurnRows.Select(row => row.Turn).ToArray();
        var auditEvents = new AuditEventRepository(connection)
            .ListAll()
            .OrderByDescending(auditEvent => auditEvent.OccurredAt)
            .Take(10)
            .ToArray();
        var recentHistory = new LocalTicketHistoryRepository(connection).ListRecentHistoryToday(10);
        var databaseSize = File.Exists(LocalAppPaths.DatabasePath)
            ? new FileInfo(LocalAppPaths.DatabasePath).Length
            : 0;

        var alerts = CalculateAlerts(activeTurnRows, now, barbers);

        return new LocalAdminSnapshot(
            now,
            LocalAppPaths.DatabasePath,
            databaseSize,
            report.Operations,
            report.Cash,
            barbers,
            services,
            ProfileImageCatalog.ListProfileImages(),
            activeTurns,
            alerts,
            auditEvents,
            recentHistory);
    }

    public void SaveBarber(
        Guid? barberId,
        string displayName,
        int rotationOrder,
        int? stationNumber,
        string? profileImagePath,
        bool isActive,
        int commissionPercentage = Barber.DefaultCommissionPercentage)
    {
        var normalizedName = NormalizeDisplayName(displayName);
        var normalizedStationNumber = NormalizeStationNumber(stationNumber, isActive);
        var normalizedProfileImagePath = NormalizeProfileImagePath(profileImagePath);
        var normalizedCommissionPercentage = NormalizeCommissionPercentage(commissionPercentage);
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

            if (!isActive)
            {
                normalizedStationNumber = null;
            }

            if (isActive && normalizedStationNumber is int activeStationNumber)
            {
                EnsureStationAvailable(barberRepository, barberId, activeStationNumber);
            }

            var newState = existingState;
            if (isActive)
            {
                if (existing is not null && !existing.IsActive)
                {
                    newState = BarberState.Available;
                }
            }
            else
            {
                newState = BarberState.Offline;
            }

            var barber = new Barber(
                barberId ?? Guid.NewGuid(),
                normalizedName,
                newState,
                existing?.ClientsServedToday ?? 0,
                rotationOrder,
                existing?.CheckedInAt,
                normalizedStationNumber,
                normalizedProfileImagePath,
                isActive,
                normalizedCommissionPercentage);

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
                    barber.IsActive,
                    barber.CommissionPercentage
                }),
                deviceId));
        });
    }

    public IReadOnlyList<ProfileImageOption> LoadProfileImages()
    {
        return ProfileImageCatalog.ListProfileImages();
    }

    public void SaveService(Guid? serviceId, string name, decimal price, bool isActive, int displayOrder)
    {
        var normalizedName = NormalizeServiceName(name);
        if (price <= 0)
        {
            throw new InvalidOperationException("Service price must be greater than zero.");
        }

        if (displayOrder < 0)
        {
            throw new InvalidOperationException("Service display order must be zero or greater.");
        }

        var now = DateTimeOffset.Now;
        var deviceId = Environment.MachineName;
        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            var serviceRepository = new ServiceRepository(connection, sqliteTransaction);
            var auditRepository = new AuditEventRepository(connection, sqliteTransaction);
            var existing = serviceId is null ? null : serviceRepository.GetById(serviceId.Value);

            if (serviceId is not null && existing is null)
            {
                throw new InvalidOperationException("Service was not found in the local database.");
            }

            var service = new Service(
                serviceId ?? Guid.NewGuid(),
                normalizedName,
                price,
                isActive,
                displayOrder,
                existing?.CreatedAt ?? now,
                now);

            if (existing is null)
            {
                serviceRepository.Add(service);
            }
            else
            {
                serviceRepository.Update(service);
            }

            auditRepository.Add(new AuditEvent(
                Guid.NewGuid(),
                now,
                serviceId is null ? "admin_service_created" : "admin_service_updated",
                "service",
                service.Id,
                JsonSerializer.Serialize(new
                {
                    serviceId = service.Id,
                    serviceName = service.Name,
                    price = service.Price,
                    service.PriceCents,
                    service.IsActive,
                    service.DisplayOrder
                }),
                deviceId));
        });
    }

    public void DeleteService(Guid serviceId)
    {
        if (serviceId == Guid.Empty)
        {
            throw new InvalidOperationException("Select a service before deleting.");
        }

        var now = DateTimeOffset.Now;
        var deviceId = Environment.MachineName;
        var transaction = new LocalDataTransaction(_connectionFactory);

        try
        {
            transaction.Execute((connection, sqliteTransaction) =>
            {
                var serviceRepository = new ServiceRepository(connection, sqliteTransaction);
                var auditRepository = new AuditEventRepository(connection, sqliteTransaction);
                var service = serviceRepository.GetById(serviceId)
                    ?? throw new InvalidOperationException("Service was not found in the local database.");

                serviceRepository.Delete(serviceId);
                auditRepository.Add(new AuditEvent(
                    Guid.NewGuid(),
                    now,
                    "admin_service_deleted",
                    "service",
                    serviceId,
                    JsonSerializer.Serialize(new
                    {
                        serviceId,
                        serviceName = service.Name
                    }),
                    deviceId));
            });
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException(
                "This service already has local payment history and cannot be deleted. Deactivate it instead.",
                exception);
        }
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

    public void SetServiceActive(Guid serviceId, bool isActive)
    {
        if (serviceId == Guid.Empty)
        {
            throw new InvalidOperationException("Select a service before changing active status.");
        }

        var now = DateTimeOffset.Now;
        var deviceId = Environment.MachineName;
        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            var serviceRepository = new ServiceRepository(connection, sqliteTransaction);
            var auditRepository = new AuditEventRepository(connection, sqliteTransaction);
            var service = serviceRepository.GetById(serviceId)
                ?? throw new InvalidOperationException("Service was not found in the local database.");

            serviceRepository.SetActive(serviceId, isActive, now);
            auditRepository.Add(new AuditEvent(
                Guid.NewGuid(),
                now,
                "admin_service_active_changed",
                "service",
                serviceId,
                JsonSerializer.Serialize(new
                {
                    serviceId,
                    serviceName = service.Name,
                    previousIsActive = service.IsActive,
                    isActive
                }),
                deviceId));
        });
    }

    public void MarkBarberAvailable(Guid barberId)
    {
        UpdateBarberState(barberId, BarberState.Available);
    }

    public void MarkBarberOffline(Guid barberId)
    {
        UpdateBarberState(barberId, BarberState.Offline);
    }

    public void ActivateBarber(Guid barberId)
    {
        throw new InvalidOperationException("Reactivate the barber from the barber editor and assign an available station.");
    }

    public void DeactivateBarber(Guid barberId)
    {
        UpdateBarberActiveState(barberId, isActive: false);
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

            if (turn.State is not (TurnState.Waiting or TurnState.Called or TurnState.InService))
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

    public void ReassignTurn(Guid turnId, Guid targetBarberId)
    {
        if (turnId == Guid.Empty)
        {
            throw new InvalidOperationException("Select a ticket before reassigning.");
        }

        if (targetBarberId == Guid.Empty)
        {
            throw new InvalidOperationException("Select a target barber before reassigning.");
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
            var targetBarber = barberRepository.GetById(targetBarberId)
                ?? throw new InvalidOperationException("Target barber was not found in the local database.");

            if (turn.State is not (TurnState.Waiting or TurnState.Called))
            {
                throw new InvalidOperationException("Only waiting or called tickets can be reassigned from Local Admin.");
            }

            if (!targetBarber.IsActive)
            {
                throw new InvalidOperationException("Target barber is inactive. Activate the barber before reassigning tickets.");
            }

            if (IsAssignedOrReservedFor(turn, targetBarberId))
            {
                throw new InvalidOperationException("Ticket is already assigned or reserved for the selected barber.");
            }

            Barber? previousBarber = null;
            if (turn.AssignedBarberId is Guid previousBarberId)
            {
                previousBarber = barberRepository.GetById(previousBarberId)
                    ?? throw new InvalidOperationException("Assigned barber was not found in the local database.");
            }

            var resultTurnState = targetBarber.State == BarberState.Available
                ? TurnState.Called
                : TurnState.Waiting;

            if (resultTurnState == TurnState.Called)
            {
                turnRepository.AssignManuallyToBarber(turnId, targetBarberId, now);
                barberRepository.SetState(targetBarberId, BarberState.Called, now);
            }
            else
            {
                turnRepository.ReserveForBarber(turnId, targetBarberId, now);
            }

            var previousBarberReleased = false;
            if (previousBarber is not null
                && previousBarber.IsActive
                && previousBarber.Id != targetBarberId
                && previousBarber.State == BarberState.Called)
            {
                barberRepository.SetState(previousBarber.Id, BarberState.Available, now);
                previousBarberReleased = true;
            }

            TurnAssignmentDecision? reassignment = null;
            if (previousBarberReleased)
            {
                reassignment = TryAssignNextWaitingTurn(
                    turnRepository,
                    barberRepository,
                    new AppointmentReservationRepository(connection, sqliteTransaction),
                    now);
            }

            auditRepository.Add(new AuditEvent(
                Guid.NewGuid(),
                now,
                "admin_turn_reassigned",
                "turn",
                turnId,
                JsonSerializer.Serialize(new
                {
                    turnId,
                    displayTicketNumber = turn.DisplayTicketNumber,
                    internalTicketNumber = turn.TicketNumber,
                    customerName = turn.CustomerName,
                    previousTurnState = turn.State.ToString(),
                    previousBarberId = previousBarber?.Id,
                    previousBarberName = previousBarber?.DisplayName,
                    targetBarberId,
                    targetBarberName = targetBarber.DisplayName,
                    targetPreviousState = targetBarber.State.ToString(),
                    resultTurnState = resultTurnState.ToString(),
                    resultAssignedBarberId = resultTurnState == TurnState.Called ? targetBarberId : (Guid?)null,
                    resultRequestedBarberIds = new[] { targetBarberId },
                    previousBarberReleased,
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
                        reason = "ticket_reassigned"
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

    private static bool IsAssignedOrReservedFor(Turn turn, Guid barberId)
    {
        if (turn.AssignedBarberId == barberId)
        {
            return true;
        }

        return turn.State == TurnState.Waiting
            && turn.RequestedBarberIds?.Count == 1
            && turn.RequestedBarberIds.Contains(barberId);
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

            // When barber becomes available, try to assign the next waiting turn
            if (state == BarberState.Available)
            {
                var turnRepository = new LocalTurnRepository(connection, sqliteTransaction);
                var appointmentRepository = new AppointmentReservationRepository(connection, sqliteTransaction);
                var reassignment = TryAssignNextWaitingTurn(
                    turnRepository,
                    barberRepository,
                    appointmentRepository,
                    now);

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
                            reason = "barber_marked_available"
                        }),
                        deviceId));
                }
            }
        });
    }

    private void UpdateBarberActiveState(Guid barberId, bool isActive)
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

            int? targetStationNumber = null;
            barberRepository.SetActive(barberId, isActive, now, targetStationNumber);
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
                    stationCode = targetStationNumber is null ? null : FormatStationCode(targetStationNumber.Value),
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

    private static string NormalizeServiceName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Service name is required.");
        }

        return name.Trim();
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
        if (isActive && stationNumber is null)
        {
            throw new InvalidOperationException("Write a station number before activating this barber.");
        }

        if (stationNumber <= 0)
        {
            throw new InvalidOperationException("Station number must be positive.");
        }

        return stationNumber;
    }

    private static int NormalizeCommissionPercentage(int commissionPercentage)
    {
        if (commissionPercentage is < 0 or > 100)
        {
            throw new InvalidOperationException("Commission percentage must be between 0 and 100.");
        }

        return commissionPercentage;
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

    public static IReadOnlyList<LocalAdminAlert> CalculateAlerts(
        IReadOnlyList<ActiveTurnRow> activeTurnRows,
        DateTimeOffset now,
        IReadOnlyList<Barber> barbers)
    {
        var alerts = new List<LocalAdminAlert>();

        var longWaitingTurns = activeTurnRows
            .Where(row => row.Turn.State == TurnState.Waiting)
            .Select(row => new { row.Turn, Elapsed = (int)(now - row.Turn.CheckedInAt).TotalMinutes })
            .Where(x => x.Elapsed > 30)
            .OrderByDescending(x => x.Elapsed)
            .ToList();

        if (longWaitingTurns.Count > 0)
        {
            var oldest = longWaitingTurns[0];
            alerts.Add(new LocalAdminAlert(
                AlertSeverity.Warning,
                $"{longWaitingTurns.Count} client{(longWaitingTurns.Count == 1 ? "" : "s")} waiting more than 30 minutes",
                $"Ticket #{oldest.Turn.DisplayTicketNumber} has been waiting for {oldest.Elapsed} minutes.",
                null,
                null,
                null,
                oldest.Elapsed));
        }

        foreach (var row in activeTurnRows.Where(r => r.Turn.State == TurnState.Called))
        {
            var elapsed = (int)(now - row.UpdatedAt).TotalMinutes;
            if (elapsed > 4)
            {
                var barberName = row.Turn.AssignedBarberId is null
                    ? "a barber"
                    : barbers.FirstOrDefault(b => b.Id == row.Turn.AssignedBarberId)?.DisplayName ?? "a barber";

                alerts.Add(new LocalAdminAlert(
                    AlertSeverity.Critical,
                    $"Ticket #{row.Turn.DisplayTicketNumber} not started",
                    $"Ticket #{row.Turn.DisplayTicketNumber} was called {elapsed} minutes ago by {barberName} and is not in service.",
                    row.Turn.DisplayTicketNumber,
                    row.Turn.Id,
                    row.Turn.AssignedBarberId,
                    elapsed));
            }
        }

        return alerts;
    }
}
