using System.Text.Json;
using Barberia.ApiClient.Sync;
using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Repositories;
using Barberia.Data.Sync;
using Barberia.Sync.Outbox;

namespace Barberia.Desktop.Services;

internal sealed class DesktopSyncService : IDisposable
{
    private const string CursorKey = "cloud_cursor";
    private const string CatalogSnapshotFingerprintKey = "catalog_snapshot_fingerprint";
    private static readonly TimeSpan NoShowGracePeriod = TimeSpan.FromMinutes(10);

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

        ApplyDueNoShows(now);
        EnqueueCatalogSnapshotIfChanged(settings, now);

        var dispatcher = new SyncOutboxDispatcher(
            new LocalSyncOutboxStore(_connectionFactory),
            new HttpCloudSyncClient(httpClient, settings.DeviceId, settings.DeviceSecret));
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

    private void ApplyPulledChanges(DesktopSyncSettings settings, string changesJson, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(changesJson);
        var root = document.RootElement;
        var changes = root.TryGetProperty("changes", out var changesElement) ? changesElement : default;
        if (changes.ValueKind != JsonValueKind.Object)
        {
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
                GetString(data, "station_code")?.Replace("B-", "") is string sc && int.TryParse(sc, out var sn) ? sn : null,
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

        var startsAt = DateTimeOffset.Parse(GetString(data, "starts_at") ?? throw new InvalidOperationException("Appointment starts_at is required."));
        var endsAt = DateTimeOffset.Parse(GetString(data, "ends_at") ?? throw new InvalidOperationException("Appointment ends_at is required."));
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

    private void ApplyDueNoShows(DateTimeOffset now)
    {
        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            var appointmentRepository = new AppointmentReservationRepository(connection, sqliteTransaction);
            var turnRepository = new LocalTurnRepository(connection, sqliteTransaction);
            var syncRecorder = new SyncOutboxRecorder(new SyncOutboxRepository(connection, sqliteTransaction));

            foreach (var appointment in appointmentRepository.ListDueForNoShow(now, NoShowGracePeriod))
            {
                if (turnRepository.GetByAppointmentId(appointment.Id) is not null)
                {
                    continue;
                }

                appointmentRepository.MarkNoShow(appointment.Id, now, now);
                syncRecorder.Enqueue(new LocalSyncEvent(
                    Guid.NewGuid(),
                    now,
                    "appointment.no_show",
                    "appointment",
                    appointment.Id,
                    JsonSerializer.Serialize(new
                    {
                        appointment_id = appointment.Id,
                        appointment_code = appointment.AppointmentCode,
                        no_show_at = now
                    }),
                    Environment.MachineName), now);
            }
        });
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



    private static DateTimeOffset? ParseNullableDate(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : DateTimeOffset.Parse(value);
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
            File.AppendAllText(
                LocalAppPaths.ErrorLogPath,
                $"[{OperationalClock.Now:O}] {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record CatalogSnapshot(string Payload, string Fingerprint);
}
