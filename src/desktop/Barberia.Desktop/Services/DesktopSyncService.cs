using System.Globalization;
using System.Text.Json;
using Barberia.ApiClient.Sync;
using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Barberia.Data.Sync;
using Barberia.Sync.Outbox;

namespace Barberia.Desktop.Services;

internal sealed class DesktopSyncService : IDisposable
{
    private const string CursorKey = "cloud_cursor";
    private const string CatalogSnapshotFingerprintKey = "catalog_snapshot_fingerprint";
    private const string PayrollSnapshotFingerprintPrefix = "payroll_snapshot_fingerprint:";

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _loopTask;

    public DesktopSyncService()
        : this(LocalDesktopDatabase.CreateConnectionFactory())
    {
    }

    public DesktopSyncService(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        new LocalDatabaseInitializer(_connectionFactory).Initialize();
    }

    public void Start()
    {
        var settings = DesktopSyncSettings.Load();
        if (settings is null)
        {
            Log("Cloud sync is not configured. Create sync-settings.json to enable it.");
            return;
        }

        _loopTask = Task.Run(() => RunLoopAsync(settings, _cancellationTokenSource.Token));
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }

    private async Task RunLoopAsync(DesktopSyncSettings settings, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.PollSeconds));

        do
        {
            try
            {
                await RunOnceAsync(settings, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                Log($"Cloud sync failed: {exception.Message}");
            }
        } while (await timer.WaitForNextTickAsync(cancellationToken));
    }

    private async Task RunOnceAsync(DesktopSyncSettings settings, CancellationToken cancellationToken)
    {
        var now = OperationalClock.Now;
        using var httpClient = new HttpClient { BaseAddress = BuildBaseUri(settings.SupabaseUrl) };
        var pullService = new CloudPullService(httpClient, settings.DeviceId, settings.DeviceSecret);
        var cursor = GetSyncState(CursorKey) ?? new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).ToString("O");
        var changesJson = await pullService.PullChangesAsync(cursor);

        if (!string.IsNullOrWhiteSpace(changesJson))
        {
            ApplyPulledChanges(settings, changesJson, now);
        }

        AppointmentStatusMaintenanceService.ApplyDueNoShows(_connectionFactory, now, Environment.MachineName);
        EnqueueCatalogSnapshotIfChanged(settings, now);
        EnqueuePayrollSnapshotsIfChanged(settings, now);

        var dispatcher = new SyncOutboxDispatcher(
            new LocalSyncOutboxStore(_connectionFactory),
            new HttpCloudSyncClient(httpClient, settings.DeviceId, settings.DeviceSecret));
        await dispatcher.DispatchDueAsync(now);
        EnqueueHeartbeat(settings, now);
        await dispatcher.DispatchDueAsync(now);

        cancellationToken.ThrowIfCancellationRequested();
    }

    private void EnqueueCatalogSnapshotIfChanged(DesktopSyncSettings settings, DateTimeOffset now)
    {
        using var connection = _connectionFactory.OpenConnection();
        var stateRepository = new SyncStateRepository(connection);
        var snapshot = BuildCatalogSnapshot(connection);
        if (string.Equals(stateRepository.GetValue(CatalogSnapshotFingerprintKey), snapshot.Fingerprint, StringComparison.Ordinal))
        {
            return;
        }

        var aggregateId = Guid.TryParse(settings.DeviceId, out var deviceGuid) ? deviceGuid : Guid.NewGuid();
        new SyncOutboxRecorder(new SyncOutboxRepository(connection)).Enqueue(
            new LocalSyncEvent(
                Guid.NewGuid(),
                now,
                "catalog.snapshot",
                "catalog",
                aggregateId,
                snapshot.Payload,
                settings.DeviceId),
            now);
        stateRepository.SetValue(CatalogSnapshotFingerprintKey, snapshot.Fingerprint, now);
    }

    private static CatalogSnapshot BuildCatalogSnapshot(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var barbers = new LocalBarberRepository(connection)
            .ListAll()
            .OrderBy(barber => barber.Id)
            .ToArray();

        var services = new ServiceRepository(connection)
            .ListAll()
            .OrderBy(service => service.Id)
            .ToArray();

        var payloadBarbers = barbers
            .Select(barber => new
            {
                entity_type = "barber",
                local_id = barber.Id.ToString(),
                display_name = barber.DisplayName,
                station_code = barber.StationCode,
                is_available_locally = barber.IsActive && barber.State != BarberState.Offline,
                updated_at = barber.UpdatedAt.ToString("O")
            });

        var payloadServices = services
            .Select(service => new
            {
                entity_type = "service",
                local_id = service.Id.ToString(),
                display_name = service.Name,
                price_cents = service.PriceCents,
                is_active = service.IsActive,
                updated_at = service.UpdatedAt.ToString("O")
            });

        var fingerprintBarbers = barbers
            .Select(barber => new
            {
                entity_type = "barber",
                local_id = barber.Id.ToString(),
                display_name = barber.DisplayName,
                station_code = barber.StationCode,
                is_available_locally = barber.IsActive && barber.State != BarberState.Offline
            });

        var fingerprintServices = services
            .Select(service => new
            {
                entity_type = "service",
                local_id = service.Id.ToString(),
                display_name = service.Name,
                price_cents = service.PriceCents,
                is_active = service.IsActive
            });

        // Fingerprints intentionally ignore updated_at, which changes when cloud echoes the same catalog row back.
        return new CatalogSnapshot(
            JsonSerializer.Serialize(new { items = payloadBarbers.Cast<object>().Concat(payloadServices) }),
            JsonSerializer.Serialize(new { items = fingerprintBarbers.Cast<object>().Concat(fingerprintServices) }));
    }

    private void EnqueuePayrollSnapshotsIfChanged(DesktopSyncSettings settings, DateTimeOffset now)
    {
        try
        {
            var payrollService = new PayrollService(_connectionFactory);
            var range = payrollService.GetWeekRange(now);
            var snapshot = payrollService.LoadOrGenerate(range.Start);
            EnqueuePayrollSnapshotIfChanged(settings, snapshot, now);

            foreach (var period in payrollService.ListHistoricalPeriods())
            {
                if (period.StartDate == snapshot.Period.StartDate && period.EndDate == snapshot.Period.EndDate)
                {
                    continue;
                }

                EnqueuePayrollSnapshotIfChanged(settings, payrollService.Load(new PayrollWeekRange(period.StartDate, period.EndDate)), now);
            }
        }
        catch (Exception exception)
        {
            Log($"Payroll snapshot skipped: {exception.Message}");
        }
    }

    private void EnqueuePayrollSnapshotIfChanged(DesktopSyncSettings settings, PayrollSnapshot snapshot, DateTimeOffset now)
    {
        using var connection = _connectionFactory.OpenConnection();
        var stateRepository = new SyncStateRepository(connection);
        var payload = PayrollSyncPayload.CreateSnapshot(snapshot);
        var fingerprint = BuildPayrollSnapshotFingerprint(snapshot);
        var fingerprintKey = $"{PayrollSnapshotFingerprintPrefix}{snapshot.Period.StartDate:yyyy-MM-dd}";
        if (string.Equals(stateRepository.GetValue(fingerprintKey), fingerprint, StringComparison.Ordinal))
        {
            return;
        }

        EnqueuePayrollSnapshot(connection, snapshot, payload, settings.DeviceId, now);
        stateRepository.SetValue(fingerprintKey, fingerprint, now);
    }

    private static string BuildPayrollSnapshotFingerprint(PayrollSnapshot snapshot)
    {
        return JsonSerializer.Serialize(new
        {
            period = new
            {
                start_date = snapshot.Period.StartDate.ToString("yyyy-MM-dd"),
                end_date = snapshot.Period.EndDate.ToString("yyyy-MM-dd"),
                state = snapshot.Period.State.ToString(),
                total_services = snapshot.Period.TotalServices,
                total_commission_cents = snapshot.Period.TotalCommissionCents,
                total_adjustments_cents = snapshot.Period.TotalAdjustmentsCents,
                total_to_pay_cents = snapshot.Period.TotalToPayCents,
                payment_method = snapshot.Period.PaymentMethod?.ToString(),
                payment_reference = snapshot.Period.PaymentReference,
                paid_at = snapshot.Period.PaidAt?.ToString("O")
            },
            lines = snapshot.Lines
                .OrderBy(line => line.BarberId)
                .Select(line => new
                {
                    barber_id = line.BarberId,
                    barber_name = line.BarberName,
                    station_number = line.StationNumber,
                    closed_services_count = line.ClosedServicesCount,
                    sales_generated_cents = line.SalesGeneratedCents,
                    commission_cents = line.CommissionCents,
                    adjustments_cents = line.AdjustmentsCents,
                    total_cents = line.TotalCents
                }),
        });
    }

    private static void EnqueuePayrollSnapshot(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        PayrollSnapshot snapshot,
        string payload,
        string deviceId,
        DateTimeOffset now,
        Microsoft.Data.Sqlite.SqliteTransaction? transaction = null)
    {
        new SyncOutboxRecorder(new SyncOutboxRepository(connection, transaction)).Enqueue(
            new LocalSyncEvent(
                Guid.NewGuid(),
                now,
                "payroll.snapshot",
                "payroll_period",
                snapshot.Period.Id,
                payload,
                deviceId),
            now);
    }

    private void EnqueueHeartbeat(DesktopSyncSettings settings, DateTimeOffset now)
    {
        using var connection = _connectionFactory.OpenConnection();
        var pendingOutboxCount = new SyncOutboxRepository(connection).CountPending();
        new SyncOutboxRecorder(new SyncOutboxRepository(connection)).Enqueue(
            new LocalSyncEvent(
                Guid.NewGuid(),
                now,
                "desktop.sync_heartbeat",
                "sync_device",
                Guid.TryParse(settings.DeviceId, out var deviceGuid) ? deviceGuid : Guid.NewGuid(),
                PayrollSyncPayload.CreateHeartbeat(pendingOutboxCount, now),
                settings.DeviceId),
            now);
    }

    private void ApplyPulledChanges(DesktopSyncSettings settings, string changesJson, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(changesJson);
        var root = document.RootElement;
        var changes = root.TryGetProperty("changes", out var changesElement) ? changesElement : default;
        if (changes.ValueKind != JsonValueKind.Object)
        {
            Log("Cloud sync response did not include a valid changes object.");
            return;
        }

        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            var appointmentRepository = new AppointmentReservationRepository(connection, sqliteTransaction);
            var syncRecorder = new SyncOutboxRecorder(new SyncOutboxRepository(connection, sqliteTransaction));
            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var serviceRepository = new ServiceRepository(connection, sqliteTransaction);

            if (changes.TryGetProperty("catalog", out var catalog)
                && catalog.ValueKind == JsonValueKind.Array)
            {
                foreach (var change in catalog.EnumerateArray())
                {
                    ApplyCatalogChange(barberRepository, serviceRepository, change);
                }
            }

            if (changes.TryGetProperty("appointments", out var appointments)
                && appointments.ValueKind == JsonValueKind.Array)
            {
                foreach (var change in appointments.EnumerateArray())
                {
                    ApplyAppointmentChange(
                        settings,
                        appointmentRepository,
                        syncRecorder,
                        change,
                        now);
                }
            }

            if (root.TryGetProperty("new_cursor", out var cursorElement)
                && cursorElement.ValueKind == JsonValueKind.String)
            {
                new SyncStateRepository(connection, sqliteTransaction)
                    .SetValue(CursorKey, cursorElement.GetString(), now);
            }
        });

        if (changes.TryGetProperty("ticket_commands", out var ticketCommands)
            && ticketCommands.ValueKind == JsonValueKind.Array)
        {
            Log($"Found {ticketCommands.GetArrayLength()} ticket command(s) in cloud sync payload.");
            var localAdminService = new LocalAdminService(_connectionFactory);
            foreach (var change in ticketCommands.EnumerateArray())
            {
                ApplyTicketCommand(settings, change, localAdminService, now);
            }
        }
        else
        {
            Log("Cloud sync payload did not include a ticket_commands array.");
        }

        if (changes.TryGetProperty("payroll_commands", out var payrollCommands)
            && payrollCommands.ValueKind == JsonValueKind.Array)
        {
            Log($"Found {payrollCommands.GetArrayLength()} payroll command(s) in cloud sync payload.");
            foreach (var change in payrollCommands.EnumerateArray())
            {
                ApplyPayrollCommand(settings, change, now);
            }
        }
        else
        {
            Log("Cloud sync payload did not include a payroll_commands array.");
        }
    }

    private static void ApplyCatalogChange(
        LocalBarberRepository barberRepository,
        ServiceRepository serviceRepository,
        JsonElement change)
    {
        var type = GetString(change, "type");
        if (!change.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return;

        if (type == "upsert_barber")
        {
            var idStr = GetString(data, "id");
            if (!Guid.TryParse(idStr, out var id)) return;
            var updatedAtStr = GetString(data, "updated_at");
            var updatedAt = ParseNullableDate(updatedAtStr) ?? DateTimeOffset.UtcNow;
            
            var existing = barberRepository.GetById(id);
            if (existing is not null && existing.UpdatedAt >= updatedAt) return;

            var isCloudActive = data.TryGetProperty("is_active", out var act) ? act.GetBoolean() : existing?.IsActive ?? true;
            var isLocallyActive = data.TryGetProperty("is_available_locally", out var ia) ? ia.GetBoolean() : true;
            var state = existing?.State ?? BarberState.Offline;
            if (!isLocallyActive)
            {
                state = BarberState.Offline;
            }
            else if (state == BarberState.Offline)
            {
                state = BarberState.NotCheckedIn;
            }

            var barber = new Barber(
                id,
                GetString(data, "display_name") ?? "Unknown",
                state,
                existing?.ClientsServedToday ?? 0,
                existing?.RotationOrder ?? 0,
                existing?.CheckedInAt,
                GetString(data, "station_code")?.Replace("B-", "", StringComparison.OrdinalIgnoreCase).Trim() is string sc && int.TryParse(sc, out var sn) ? sn : null,
                existing?.ProfileImagePath,
                isActive: isCloudActive,
                existing?.CommissionPercentage ?? Barber.DefaultCommissionPercentage,
                updatedAt
            );
            barberRepository.Upsert(barber, updatedAt);
        }
        else if (type == "upsert_service")
        {
            var idStr = GetString(data, "id");
            if (!Guid.TryParse(idStr, out var id)) return;
            var updatedAtStr = GetString(data, "updated_at");
            var updatedAt = ParseNullableDate(updatedAtStr) ?? DateTimeOffset.UtcNow;

            var existing = serviceRepository.GetById(id);
            if (existing is not null && existing.UpdatedAt >= updatedAt) return;

            data.TryGetProperty("base_price_cents", out var pcElement);
            var pc = pcElement.ValueKind == JsonValueKind.Number ? pcElement.GetInt64() : 0;
            var createdAtStr = GetString(data, "created_at");

            var service = new Barberia.Data.Models.Service(
                id,
                GetString(data, "name") ?? "Unknown",
                pc,
                data.TryGetProperty("is_active", out var ia) ? ia.GetBoolean() : true,
                existing?.DisplayOrder ?? 0,
                ParseNullableDate(createdAtStr) ?? DateTimeOffset.UtcNow,
                updatedAt
            );

            if (existing is null)
            {
                serviceRepository.Add(service);
            }
            else
            {
                serviceRepository.Update(service);
            }
        }
    }

    private void ApplyTicketCommand(
        DesktopSyncSettings settings,
        JsonElement change,
        LocalAdminService localAdminService,
        DateTimeOffset now)
    {
        var type = GetString(change, "type");
        Log($"Received ticket command type: {type ?? "(missing)"}.");

        if (type is not ("ticket.reassign" or "ticket.cancel") || !change.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            Log("Ticket command dropped because it is not ticket.reassign or ticket.cancel or has no data object.");
            return;
        }

        var commandIdText = GetString(data, "id");
        if (!Guid.TryParse(commandIdText, out var commandId))
        {
            Log($"Ticket command dropped because command id is invalid: '{commandIdText}'.");
            return;
        }

        var syncStateKey = $"cloud_ticket_command:{commandId}";
        if (GetSyncState(syncStateKey) is not null)
        {
            Log($"Ticket command {commandId} skipped because it was already processed.");
            return;
        }

        var localTicketIdText = GetString(data, "local_ticket_id");
        var targetBarberIdText = GetString(data, "target_barber_id");

        Log($"Processing ticket command {commandId}. Ticket: {localTicketIdText}, Target barber: {targetBarberIdText}.");

        if (!Guid.TryParse(localTicketIdText, out var turnId))
        {
            Log($"Ticket command {commandId} dropped because ticket id is invalid.");
            return;
        }

        bool success = false;
        string? errorMessage = null;
        try
        {
            if (type == "ticket.cancel")
            {
                localAdminService.CancelTurn(turnId);
            }
            else
            {
                if (!Guid.TryParse(targetBarberIdText, out var targetBarberId))
                {
                    throw new InvalidOperationException("Target barber id is invalid.");
                }
                localAdminService.ReassignTurn(turnId, targetBarberId);
            }
            success = true;
            Log($"Ticket command {commandId} applied successfully.");
        }
        catch (Exception ex)
        {
            success = false;
            errorMessage = ex.Message;
            Log($"Ticket command {commandId} failed: {ex.Message}");
        }

        try
        {
            var transaction = new LocalDataTransaction(_connectionFactory);
            transaction.Execute((connection, sqliteTransaction) =>
            {
                new SyncStateRepository(connection, sqliteTransaction)
                    .SetValue(syncStateKey, "processed", now);

                var ackEvent = new LocalSyncEvent(
                    Guid.NewGuid(),
                    now,
                    success ? "ticket_admin_command.applied" : "ticket_admin_command.failed",
                    "ticket_admin_command",
                    commandId,
                    JsonSerializer.Serialize(new
                    {
                        command_id = commandId,
                        status = success ? "applied" : "failed",
                        error_message = errorMessage
                    })
                );

                new SyncOutboxRecorder(new SyncOutboxRepository(connection, sqliteTransaction)).Enqueue(ackEvent, now);
            });
            Log($"Ack enqueued for ticket command {commandId}.");
        }
        catch (Exception ex)
        {
            Log($"Error saving ack for ticket command {commandId}: {ex.Message}");
        }
    }

    private static void ApplyAppointmentChange(
        DesktopSyncSettings settings,
        AppointmentReservationRepository appointmentRepository,
        SyncOutboxRecorder syncRecorder,
        JsonElement change,
        DateTimeOffset now)
    {
        if (!change.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var appointmentIdText = GetString(data, "id");
        if (!Guid.TryParse(appointmentIdText, out var appointmentId))
        {
            return;
        }

        var existing = appointmentRepository.GetById(appointmentId);
        var cloudBarberId = GetString(data, "barber_id");
        var cloudServiceId = GetString(data, "service_id");
        var localBarberText = cloudBarberId;
        var localServiceText = cloudServiceId;

        if (localBarberText is null && existing is not null)
        {
            localBarberText = existing.BarberId.ToString();
        }

        if (localServiceText is null && existing?.ServiceId is Guid existingServiceId)
        {
            localServiceText = existingServiceId.ToString();
        }

        if (!Guid.TryParse(localBarberText, out var localBarberId)
            || !Guid.TryParse(localServiceText, out var localServiceId))
        {
            EnqueueMissingMappingConflict(settings, syncRecorder, appointmentId, data, now);
            return;
        }

        var startsAt = OperationalClock.ToLocalTime(DateTimeOffset.Parse(GetString(data, "starts_at") ?? throw new InvalidOperationException("Appointment starts_at is required.")));
        var endsAt = OperationalClock.ToLocalTime(DateTimeOffset.Parse(GetString(data, "ends_at") ?? throw new InvalidOperationException("Appointment ends_at is required.")));
        var status = GetString(data, "status") ?? "confirmed";
        var customerName = GetNestedString(data, "customer", "display_name");
        var appointment = new AppointmentReservation(
            appointmentId,
            localBarberId,
            ToLocalState(status, GetString(data, "checked_in_at"), GetString(data, "completed_at"), GetString(data, "no_show_at")),
            startsAt,
            AppointmentReservation.DefaultProtectionWindow,
            localServiceId,
            endsAt,
            GetString(data, "appointment_code"),
            customerName,
            ParseNullableDate(GetString(data, "checked_in_at")),
            ParseNullableDate(GetString(data, "no_show_at")),
            ParseNullableDate(GetString(data, "completed_at")));

        appointmentRepository.Upsert(appointment, now);
    }

    private void ApplyPayrollCommand(
        DesktopSyncSettings settings,
        JsonElement change,
        DateTimeOffset now)
    {
        var type = GetString(change, "type");
        Log($"Received payroll command type: {type ?? "(missing)"}.");

        if (type is not ("payroll.snapshot_requested" or "payroll.adjustment_added" or "payroll.pay_requested")
            || !change.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Object)
        {
            Log("Payroll command dropped because it has no supported type or data object.");
            return;
        }

        var commandIdText = GetString(data, "id");
        if (!Guid.TryParse(commandIdText, out var commandId))
        {
            Log($"Payroll command dropped because command id is invalid: '{commandIdText}'.");
            return;
        }

        var syncStateKey = $"cloud_payroll_command:{commandId}";
        if (GetSyncState(syncStateKey) is not null)
        {
            Log($"Payroll command {commandId} skipped because it was already processed.");
            return;
        }

        var success = false;
        string? errorMessage = null;
        PayrollSnapshot? snapshot = null;

        try
        {
            var range = ParsePayrollRange(data);
            if (type == "payroll.adjustment_added")
            {
                throw new InvalidOperationException("Manual payroll adjustments are no longer supported.");
            }
            else if (type == "payroll.pay_requested")
            {
                EnsurePayrollCanBePaidLocally(range, now);
            }

            var payrollService = new PayrollService(_connectionFactory);
            if (type == "payroll.pay_requested")
            {
                snapshot = payrollService.PayPeriod(
                    range,
                    ParsePayrollPaymentMethod(GetPayloadString(data, "payment_method")),
                    GetPayloadString(data, "payment_reference"),
                    GetPayloadString(data, "notes"),
                    now);
            }
            else
            {
                snapshot = payrollService.LoadOrGenerate(range.Start);
            }

            success = true;
            Log($"Payroll command {commandId} applied successfully.");
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            Log($"Payroll command {commandId} failed: {ex.Message}");
        }

        try
        {
            var transaction = new LocalDataTransaction(_connectionFactory);
            transaction.Execute((connection, sqliteTransaction) =>
            {
                new SyncStateRepository(connection, sqliteTransaction)
                    .SetValue(syncStateKey, "processed", now);

                if (snapshot is not null)
                {
                    EnqueuePayrollSnapshot(
                        connection,
                        snapshot,
                        PayrollSyncPayload.CreateSnapshot(snapshot),
                        settings.DeviceId,
                        now,
                        sqliteTransaction);
                }

                var ackEvent = new LocalSyncEvent(
                    Guid.NewGuid(),
                    now,
                    success ? "payroll_admin_command.applied" : "payroll_admin_command.failed",
                    "payroll_admin_command",
                    commandId,
                    PayrollSyncPayload.CreateCommandAck(commandId, success, errorMessage),
                    settings.DeviceId);

                new SyncOutboxRecorder(new SyncOutboxRepository(connection, sqliteTransaction)).Enqueue(ackEvent, now);
            });
            Log($"Ack enqueued for payroll command {commandId}.");
        }
        catch (Exception ex)
        {
            Log($"Error saving ack for payroll command {commandId}: {ex.Message}");
        }
    }

    private void EnsurePayrollCanBePaidLocally(PayrollWeekRange range, DateTimeOffset now)
    {
        if (range.End > now)
        {
            throw new InvalidOperationException("Payroll period has not closed yet.");
        }

        using var connection = _connectionFactory.OpenConnection();
        var pendingOutboxCount = new SyncOutboxRepository(connection).CountPending();
        if (pendingOutboxCount > 0)
        {
            throw new InvalidOperationException("Desktop has pending sync events. Payroll payment must wait until sync is clean.");
        }
    }

    private static void EnqueueMissingMappingConflict(
        DesktopSyncSettings settings,
        SyncOutboxRecorder syncRecorder,
        Guid appointmentId,
        JsonElement appointmentData,
        DateTimeOffset now)
    {
        syncRecorder.Enqueue(new LocalSyncEvent(
            Guid.NewGuid(),
            now,
            "sync.conflict",
            "appointment",
            appointmentId,
            JsonSerializer.Serialize(new
            {
                conflict_type = "missing_catalog_mapping",
                local_payload = new
                {
                    reason = "Desktop cannot map cloud appointment barber/service to local catalog ids.",
                    device_id = settings.DeviceId
                },
                cloud_payload = appointmentData
            }),
            Environment.MachineName), now);
    }

    private string? GetSyncState(string key)
    {
        using var connection = _connectionFactory.OpenConnection();
        return new SyncStateRepository(connection).GetValue(key);
    }

    private static AppointmentState ToLocalState(string status, string? checkedInAt, string? completedAt, string? noShowAt)
    {
        if (!string.IsNullOrWhiteSpace(completedAt) || status == "completed")
        {
            return AppointmentState.Completed;
        }

        if (!string.IsNullOrWhiteSpace(noShowAt) || status == "no_show")
        {
            return AppointmentState.NoShow;
        }

        if (status == "cancelled")
        {
            return AppointmentState.Cancelled;
        }

        if (!string.IsNullOrWhiteSpace(checkedInAt))
        {
            return AppointmentState.CheckedIn;
        }

        return AppointmentState.Confirmed;
    }

    private static PayrollWeekRange ParsePayrollRange(JsonElement data)
    {
        var startDateText = GetString(data, "start_date") ?? GetPayloadString(data, "start_date");
        var endDateText = GetString(data, "end_date") ?? GetPayloadString(data, "end_date");
        if (string.IsNullOrWhiteSpace(startDateText) || string.IsNullOrWhiteSpace(endDateText))
        {
            throw new InvalidOperationException("Payroll command must include start_date and end_date.");
        }

        var startDate = ParsePayrollDate(startDateText);
        var endDate = ParsePayrollDate(endDateText);
        if (endDate <= startDate)
        {
            throw new InvalidOperationException("Payroll command date range is invalid.");
        }

        return new PayrollWeekRange(startDate, endDate);
    }

    private static DateTimeOffset ParsePayrollDate(string value)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)
            && value.Contains('T', StringComparison.Ordinal))
        {
            return new DateTimeOffset(timestamp.Date, timestamp.Offset);
        }

        var date = DateTime.Parse(value, CultureInfo.InvariantCulture).Date;
        return new DateTimeOffset(date, OperationalClock.Now.Offset);
    }

    private static PayrollPaymentMethod ParsePayrollPaymentMethod(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "transfer" => PayrollPaymentMethod.Transfer,
            "other" => PayrollPaymentMethod.Other,
            _ => PayrollPaymentMethod.Cash
        };
    }

    private static string? GetPayloadString(JsonElement data, string propertyName)
    {
        if (data.TryGetProperty("payload", out var payload)
            && payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return GetString(data, propertyName);
    }

    private static DateTimeOffset? ParseNullableDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            return OperationalClock.ToLocalTime(parsed);
        }
        return null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? GetNestedString(JsonElement element, string propertyName, string nestedPropertyName)
    {
        return element.TryGetProperty(propertyName, out var nested)
            && nested.ValueKind == JsonValueKind.Object
            ? GetString(nested, nestedPropertyName)
            : null;
    }

    private static Uri BuildBaseUri(string supabaseUrl)
    {
        return new Uri(supabaseUrl.TrimEnd('/') + "/");
    }

    private static void Log(string message)
    {
        try
        {
            var logMessage = $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}";
            File.AppendAllText(LocalAppPaths.ErrorLogPath, logMessage);
        }
        catch { }

        try
        {
            File.AppendAllText(@"C:\temp\sync_debug.log", $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    private sealed record CatalogSnapshot(string Payload, string Fingerprint);
}

