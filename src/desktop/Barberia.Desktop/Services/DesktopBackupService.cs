using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Barberia.Data;
using Barberia.Data.Sync;
using Barberia.Sync.Outbox;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Data.Sqlite;
using Timer = System.Timers.Timer;

namespace Barberia.Desktop.Services;

internal sealed class DesktopBackupService : IDisposable
{
    private static readonly string[] RequiredRestoreTables =
    [
        "barbers",
        "turns",
        "cash_payments",
        "services",
        "sync_outbox_events",
        "sync_state"
    ];

    private readonly Timer _timer;
    private readonly HttpClient _httpClient;
    private readonly string _rootDirectory;
    private readonly string _databasePath;
    private readonly Func<SqliteConnectionFactory> _connectionFactoryProvider;
    private readonly Func<DesktopSyncSettings?> _syncSettingsProvider;
    private bool _isDisposed;
    private bool _isRunning;
    private DateOnly _lastRunDate;

    public DesktopBackupService()
        : this(
            LocalAppPaths.RootDirectory,
            LocalAppPaths.DatabasePath,
            LocalDesktopDatabase.CreateConnectionFactory,
            DesktopSyncSettings.Load)
    {
    }

    internal DesktopBackupService(
        string rootDirectory,
        string databasePath,
        Func<SqliteConnectionFactory> connectionFactoryProvider,
        Func<DesktopSyncSettings?>? syncSettingsProvider = null)
    {
        _rootDirectory = rootDirectory;
        _databasePath = databasePath;
        _connectionFactoryProvider = connectionFactoryProvider;
        _syncSettingsProvider = syncSettingsProvider ?? DesktopSyncSettings.Load;
        _timer = new Timer(TimeSpan.FromMinutes(1).TotalMilliseconds);
        _timer.Elapsed += async (s, e) => await CheckAndRunBackupAsync();
        
        _httpClient = new HttpClient();
    }

    public void Start()
    {
        _timer.Start();
        // Run an initial check just in case it missed the window
        _ = Task.Run(CheckAndRunBackupAsync);
    }

    public async Task RunManualBackupAsync()
    {
        var settings = DesktopBackupSettings.Load();
        var now = OperationalClock.Now;
        var today = OperationalClock.GetBusinessDate(now);
        await PerformBackupAsync(settings, today);
    }

    public Task<DesktopBackupRestoreResult> RestoreBackupAsync(
        string zipPath,
        string? passwordOverride = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);

        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException("Backup file was not found.", zipPath);
        }

        if (!string.Equals(Path.GetExtension(zipPath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Backup restore only supports .zip files.");
        }

        EnsureNoPendingSyncEvents();

        var settings = DesktopBackupSettings.Load();
        var tempDirectory = Path.Combine(_rootDirectory, "restore-temp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var extractedDatabasePath = ExtractDatabaseFromBackup(zipPath, tempDirectory, settings, passwordOverride);
            ValidateRestoredDatabase(extractedDatabasePath);
            EnsureNoPendingSyncEvents(extractedDatabasePath, "restored backup");

            EnsureNoPendingSyncEvents();
            var safetyBackupPath = CreatePreRestoreBackup(settings);
            var restoreId = EnqueueRestoreAppliedEvent(extractedDatabasePath, zipPath, safetyBackupPath);
            SqliteConnection.ClearAllPools();
            ReplaceCurrentDatabase(extractedDatabasePath);

            Log($"Restored database backup from {zipPath}. Restore id: {restoreId}. Safety backup: {safetyBackupPath}");
            return Task.FromResult(new DesktopBackupRestoreResult(safetyBackupPath, restoreId, RestartRequired: true));
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private async Task CheckAndRunBackupAsync()
    {
        if (_isRunning) return;

        try
        {
            _isRunning = true;

            var settings = DesktopBackupSettings.Load();
            if (!settings.Enabled) return;

            var now = OperationalClock.Now;
            var today = OperationalClock.GetBusinessDate(now);

            // Don't run twice on the same business day
            if (_lastRunDate == today) return;

            if (TimeOnly.TryParse(settings.TimeOfDay, out var configuredTime))
            {
                var currentTime = TimeOnly.FromDateTime(now.DateTime);
                
                // If the current time has passed the configured time, we run the backup
                if (currentTime >= configuredTime)
                {
                    await PerformBackupAsync(settings, today);
                    _lastRunDate = today;
                }
            }
            else
            {
                Log($"Invalid TimeOfDay configured in backup settings: {settings.TimeOfDay}");
            }
        }
        catch (Exception ex)
        {
            Log($"Error in CheckAndRunBackupAsync: {ex}");
        }
        finally
        {
            _isRunning = false;
        }
    }

    private async Task PerformBackupAsync(DesktopBackupSettings settings, DateOnly businessDate)
    {
        var syncSettings = _syncSettingsProvider();
        var deviceId = syncSettings?.DeviceId ?? "unknown-device";
        
        var backupDirectory = Path.Combine(_rootDirectory, "backups");
        Directory.CreateDirectory(backupDirectory);

        var timestamp = OperationalClock.Now.ToString("yyyyMMdd_HHmmss");
        var baseFileName = $"{deviceId}_{timestamp}";
        var dbTempPath = Path.Combine(backupDirectory, $"{baseFileName}.db");
        var zipPath = Path.Combine(backupDirectory, $"{baseFileName}.zip");

        try
        {
            // 1. Safe SQLite copy via VACUUM INTO
            var connectionFactory = _connectionFactoryProvider();
            using (var connection = connectionFactory.OpenConnection())
            {
                using var command = connection.CreateCommand();
                command.CommandText = "VACUUM INTO $path";
                command.Parameters.AddWithValue("$path", dbTempPath);
                command.ExecuteNonQuery();
            }

            // 2. Compress and encrypt with SharpZipLib
            CreateProtectedZip(dbTempPath, zipPath, settings.EncryptedPassword);

            // 3. Delete the temporary DB file
            if (File.Exists(dbTempPath))
            {
                File.Delete(dbTempPath);
            }

            // 4. Clean up old local backups
            CleanupLocalBackups(backupDirectory, settings.LocalRetentionDays);

            // 5. Upload to Supabase
            if (syncSettings != null)
            {
                await UploadToSupabaseAsync(syncSettings, zipPath, deviceId, businessDate);
            }
            else
            {
                Log("Sync settings not found. Backup saved locally but cloud upload skipped.");
            }
        }
        catch (Exception ex)
        {
            Log($"Backup failed: {ex.Message}");
            if (File.Exists(dbTempPath)) File.Delete(dbTempPath);
            // We intentionally don't throw to prevent crashing the background thread
        }
    }

    private void CreateProtectedZip(string sourceFile, string zipPath, string? encryptedPassword)
    {
        CreateZip(sourceFile, zipPath, DecryptStoredPassword(encryptedPassword));
    }

    private static void CreateZip(string sourceFile, string zipPath, string? plainPassword)
    {
        using var fsOut = File.Create(zipPath);
        using var zipStream = new ZipOutputStream(fsOut);
        
        zipStream.SetLevel(9); // 0-9, 9 being the highest level of compression

        if (!string.IsNullOrEmpty(plainPassword))
        {
            zipStream.Password = plainPassword;
        }

        var entryName = Path.GetFileName(sourceFile);
        var newEntry = new ZipEntry(entryName)
        {
            DateTime = OperationalClock.Now.DateTime
        };
        
        zipStream.PutNextEntry(newEntry);

        using var fsIn = File.OpenRead(sourceFile);
        fsIn.CopyTo(zipStream);
        
        zipStream.CloseEntry();
    }

    private string ExtractDatabaseFromBackup(
        string zipPath,
        string tempDirectory,
        DesktopBackupSettings settings,
        string? passwordOverride)
    {
        var entryName = GetSingleDatabaseEntryName(zipPath);
        var candidatePasswords = BuildCandidatePasswords(settings, passwordOverride);
        var sawPasswordFailure = false;

        foreach (var password in candidatePasswords)
        {
            var extractedPath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.db");
            try
            {
                ExtractEntry(zipPath, entryName, extractedPath, password);
                return extractedPath;
            }
            catch (ZipException ex) when (IsPasswordFailure(ex))
            {
                sawPasswordFailure = true;
                TryDeleteFile(extractedPath);
            }
            catch (CryptographicException)
            {
                sawPasswordFailure = true;
                TryDeleteFile(extractedPath);
            }
            catch
            {
                TryDeleteFile(extractedPath);
                throw;
            }
        }

        if (sawPasswordFailure)
        {
            throw new DesktopBackupPasswordRequiredException("Backup password is required or incorrect.");
        }

        throw new InvalidOperationException("Backup could not be extracted.");
    }

    private static string GetSingleDatabaseEntryName(string zipPath)
    {
        using var zipFile = new ZipFile(zipPath);
        var databaseEntries = zipFile
            .Cast<ZipEntry>()
            .Where(entry => entry.IsFile && string.Equals(Path.GetExtension(entry.Name), ".db", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (databaseEntries.Count != 1)
        {
            throw new InvalidOperationException("Backup ZIP must contain exactly one .db file.");
        }

        return databaseEntries[0].Name;
    }

    private static void ExtractEntry(string zipPath, string entryName, string destinationPath, string? password)
    {
        using var zipFile = new ZipFile(zipPath);
        if (!string.IsNullOrEmpty(password))
        {
            zipFile.Password = password;
        }

        var entry = zipFile.GetEntry(entryName)
            ?? throw new InvalidOperationException("Backup database entry was not found.");

        using var input = zipFile.GetInputStream(entry);
        using var output = File.Create(destinationPath);
        input.CopyTo(output);
    }

    private static IReadOnlyList<string?> BuildCandidatePasswords(
        DesktopBackupSettings settings,
        string? passwordOverride)
    {
        var passwords = new List<string?>();
        if (!string.IsNullOrWhiteSpace(passwordOverride))
        {
            passwords.Add(passwordOverride);
        }

        var storedPassword = DecryptStoredPassword(settings.EncryptedPassword);
        if (!string.IsNullOrEmpty(storedPassword)
            && !passwords.Contains(storedPassword, StringComparer.Ordinal))
        {
            passwords.Add(storedPassword);
        }

        passwords.Add(null);
        return passwords;
    }

    private static string? DecryptStoredPassword(string? encryptedPassword)
    {
        if (string.IsNullOrEmpty(encryptedPassword))
        {
            return null;
        }

        try
        {
            var decryptedBytes = ProtectedData.Unprotect(
                Convert.FromBase64String(encryptedPassword),
                null,
                DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private void ValidateRestoredDatabase(string databasePath)
    {
        var connectionFactory = CreateConnectionFactory(databasePath);
        using var connection = connectionFactory.OpenConnection();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA integrity_check;";
            var result = Convert.ToString(command.ExecuteScalar());
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Backup database failed integrity check: {result}");
            }
        }

        LocalDatabaseInitializer.Initialize(connection);

        foreach (var table in RequiredRestoreTables)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name = $name;
                """;
            command.Parameters.AddWithValue("$name", table);
            if (Convert.ToInt32(command.ExecuteScalar()) != 1)
            {
                throw new InvalidOperationException($"Backup database is missing required table '{table}'.");
            }
        }
    }

    private void EnsureNoPendingSyncEvents()
    {
        if (!File.Exists(_databasePath))
        {
            return;
        }

        using var connection = _connectionFactoryProvider().OpenConnection();
        if (!TableExists(connection, "sync_outbox_events"))
        {
            return;
        }

        var pendingCount = new SyncOutboxRepository(connection).CountPending();
        if (pendingCount > 0)
        {
            throw new InvalidOperationException(
                $"Restore is blocked because this Desktop has {pendingCount} pending sync event(s). Wait for sync to finish before restoring.");
        }
    }

    private static void EnsureNoPendingSyncEvents(string databasePath, string label)
    {
        if (!File.Exists(databasePath))
        {
            return;
        }

        using var connection = CreateConnectionFactory(databasePath).OpenConnection();
        if (!TableExists(connection, "sync_outbox_events"))
        {
            return;
        }

        var pendingCount = new SyncOutboxRepository(connection).CountPending();
        if (pendingCount > 0)
        {
            throw new InvalidOperationException(
                $"Restore is blocked because the {label} contains {pendingCount} pending sync event(s). Use a backup with clean sync state before restoring.");
        }
    }

    private Guid EnqueueRestoreAppliedEvent(string restoredDatabasePath, string sourceZipPath, string safetyBackupPath)
    {
        var syncSettings = _syncSettingsProvider()
            ?? throw new InvalidOperationException("Cloud sync must be configured before restoring authoritatively to Web.");

        if (!Guid.TryParse(syncSettings.DeviceId, out var deviceId))
        {
            throw new InvalidOperationException("Configured sync device id is not a valid UUID.");
        }

        var restoreId = Guid.NewGuid();
        var restoredAt = OperationalClock.Now;
        var payload = BuildRestoreAppliedPayload(
            restoredDatabasePath,
            restoreId,
            restoredAt,
            syncSettings.DeviceId,
            sourceZipPath,
            safetyBackupPath);

        using var connection = CreateConnectionFactory(restoredDatabasePath).OpenConnection();
        new SyncOutboxRecorder(new SyncOutboxRepository(connection)).Enqueue(
            new LocalSyncEvent(
                Guid.NewGuid(),
                restoredAt,
                "desktop.restore_applied",
                "desktop_restore",
                restoreId,
                JsonSerializer.Serialize(payload),
                syncSettings.DeviceId),
            restoredAt);

        return restoreId;
    }

    private static object BuildRestoreAppliedPayload(
        string restoredDatabasePath,
        Guid restoreId,
        DateTimeOffset restoredAt,
        string deviceId,
        string sourceZipPath,
        string safetyBackupPath)
    {
        using var connection = CreateConnectionFactory(restoredDatabasePath).OpenConnection();
        var tickets = LoadRestoreTickets(connection);
        var items = LoadRestoreTicketItems(connection);
        var payments = LoadRestorePayments(connection);
        var sourceZip = new FileInfo(sourceZipPath);

        return new
        {
            restore_id = restoreId,
            restored_at = restoredAt.ToString("O"),
            device_id = deviceId,
            backup = new
            {
                file_name = sourceZip.Name,
                size_bytes = sourceZip.Exists ? sourceZip.Length : 0,
                safety_backup_path = safetyBackupPath
            },
            snapshot = new
            {
                tickets,
                ticket_items = items,
                payments
            }
        };
    }

    private static IReadOnlyList<RestoreTicketSnapshot> LoadRestoreTickets(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, display_ticket_number, ticket_date, customer_name, state, checked_in_at,
                   assigned_barber_id, appointment_id, started_at, completed_at, cancelled_at
            FROM turns
            ORDER BY checked_in_at, id;
            """;

        using var reader = command.ExecuteReader();
        var tickets = new List<RestoreTicketSnapshot>();
        while (reader.Read())
        {
            tickets.Add(new RestoreTicketSnapshot(
                LocalTicketId: reader.GetString(0),
                DisplayTicketNumber: reader.IsDBNull(1) ? null : reader.GetInt32(1),
                TicketDate: reader.IsDBNull(2) ? null : reader.GetString(2),
                CustomerName: reader.IsDBNull(3) ? null : reader.GetString(3),
                Status: ToCloudTicketStatus(reader.GetInt32(4)),
                CheckedInAt: reader.GetString(5),
                BarberId: reader.IsDBNull(6) ? null : reader.GetString(6),
                AppointmentId: reader.IsDBNull(7) ? null : reader.GetString(7),
                StartedAt: reader.IsDBNull(8) ? null : reader.GetString(8),
                CompletedAt: reader.IsDBNull(9) ? null : reader.GetString(9),
                CancelledAt: reader.IsDBNull(10) ? null : reader.GetString(10)));
        }

        return tickets;
    }

    private static IReadOnlyList<RestoreTicketItemSnapshot> LoadRestoreTicketItems(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, turn_id, service_id, COALESCE(service_price_cents, amount_cents - additional_cents, amount_cents)
            FROM cash_payments
            WHERE service_id IS NOT NULL;
            """;

        using var reader = command.ExecuteReader();
        var items = new List<RestoreTicketItemSnapshot>();
        while (reader.Read())
        {
            items.Add(new RestoreTicketItemSnapshot(
                LocalItemId: $"{reader.GetString(0)}:service",
                LocalTicketId: reader.GetString(1),
                ServiceId: reader.GetString(2),
                PriceCents: reader.GetInt64(3)));
        }

        return items;
    }

    private static IReadOnlyList<RestorePaymentSnapshot> LoadRestorePayments(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, turn_id, payment_method, amount_cents, receipt_number, payment_reference, collected_at
            FROM cash_payments
            ORDER BY collected_at, id;
            """;

        using var reader = command.ExecuteReader();
        var payments = new List<RestorePaymentSnapshot>();
        while (reader.Read())
        {
            payments.Add(new RestorePaymentSnapshot(
                LocalPaymentId: reader.GetString(0),
                LocalTicketId: reader.GetString(1),
                PaymentMethod: ToCloudPaymentMethod(reader.GetInt32(2)),
                AmountCents: reader.GetInt64(3),
                ReceiptNumber: reader.IsDBNull(4) ? null : reader.GetString(4),
                PaymentReference: reader.IsDBNull(5) ? null : reader.GetString(5),
                CollectedAt: reader.GetString(6)));
        }

        return payments;
    }

    private static string ToCloudTicketStatus(int state)
    {
        return state switch
        {
            0 => "waiting",
            2 => "called",
            3 => "in_progress",
            4 => "completed",
            6 => "no_show",
            _ => "cancelled"
        };
    }

    private static string ToCloudPaymentMethod(int paymentMethod)
    {
        return paymentMethod == 1 ? "zelle" : "cash";
    }

    private string CreatePreRestoreBackup(DesktopBackupSettings settings)
    {
        if (!File.Exists(_databasePath))
        {
            throw new InvalidOperationException("Current local database was not found. Restore was not applied.");
        }

        var backupDirectory = Path.Combine(_rootDirectory, "backups");
        Directory.CreateDirectory(backupDirectory);

        var timestamp = OperationalClock.Now.ToString("yyyyMMdd_HHmmss");
        var baseFileName = $"pre-restore_{timestamp}";
        var dbTempPath = Path.Combine(backupDirectory, $"{baseFileName}.db");
        var zipPath = Path.Combine(backupDirectory, $"{baseFileName}.zip");

        using (var connection = _connectionFactoryProvider().OpenConnection())
        {
            using var command = connection.CreateCommand();
            command.CommandText = "VACUUM INTO $path";
            command.Parameters.AddWithValue("$path", dbTempPath);
            command.ExecuteNonQuery();
        }

        try
        {
            CreateProtectedZip(dbTempPath, zipPath, settings.EncryptedPassword);
            return zipPath;
        }
        finally
        {
            TryDeleteFile(dbTempPath);
        }
    }

    private void ReplaceCurrentDatabase(string restoredDatabasePath)
    {
        var replacementPath = Path.Combine(
            Path.GetDirectoryName(_databasePath) ?? _rootDirectory,
            $"restore-{Guid.NewGuid():N}.db");
        File.Copy(restoredDatabasePath, replacementPath, overwrite: true);

        try
        {
            if (File.Exists(_databasePath))
            {
                var replacedBackupPath = Path.Combine(
                    Path.GetDirectoryName(_databasePath) ?? _rootDirectory,
                    $"restore-replaced-{OperationalClock.Now:yyyyMMdd_HHmmss}.db");
                File.Replace(replacementPath, _databasePath, replacedBackupPath, ignoreMetadataErrors: true);
                TryDeleteFile(replacedBackupPath);
            }
            else
            {
                File.Move(replacementPath, _databasePath);
            }
        }
        catch (IOException ex)
        {
            TryDeleteFile(replacementPath);
            throw new IOException("Could not replace the local database. Close other Barberia windows or processes and try again.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            TryDeleteFile(replacementPath);
            throw new UnauthorizedAccessException("Could not replace the local database because access was denied.", ex);
        }
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name = $name;
            """;
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private static SqliteConnectionFactory CreateConnectionFactory(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        };

        return new SqliteConnectionFactory(builder.ToString());
    }

    private static bool IsPasswordFailure(ZipException exception)
    {
        return exception.Message.Contains("password", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("Invalid password", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("Wrong password", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void CleanupLocalBackups(string backupDirectory, int retentionDays)
    {
        try
        {
            var cutoffDate = OperationalClock.Now.AddDays(-retentionDays);
            var files = Directory.GetFiles(backupDirectory, "*.zip");

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.CreationTime < cutoffDate)
                {
                    fileInfo.Delete();
                    Log($"Deleted old local backup: {fileInfo.Name}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to clean up local backups: {ex.Message}");
        }
    }

    private async Task UploadToSupabaseAsync(DesktopSyncSettings syncSettings, string zipPath, string deviceId, DateOnly date)
    {
        var url = $"{syncSettings.SupabaseUrl}/functions/v1/desktop-db-backups";
        
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", syncSettings.DeviceSecret);
        request.Headers.Add("x-device-id", deviceId);
        
        // Ensure path uses forward slashes and valid format: deviceId/yyyy/MM/dd/filename.zip
        var remoteFileName = Path.GetFileName(zipPath);
        var remotePath = $"{deviceId}/{date.Year:D4}/{date.Month:D2}/{date.Day:D2}/{remoteFileName}";
        request.Headers.Add("x-remote-path", remotePath);

        using var fs = File.OpenRead(zipPath);
        request.Content = new StreamContent(fs);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

        try
        {
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Log($"Cloud backup failed with status {response.StatusCode}: {error}");
            }
            else
            {
                Log($"Successfully uploaded cloud backup to {remotePath}");
            }
        }
        catch (Exception ex)
        {
            Log($"Network error uploading backup: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        try
        {
            File.AppendAllText(
                LocalAppPaths.ErrorLogPath,
                $"[{OperationalClock.Now:O}] [BackupService] {message}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _timer.Stop();
        _timer.Dispose();
        _httpClient.Dispose();
        _isDisposed = true;
    }
}

internal sealed record DesktopBackupRestoreResult(string SafetyBackupPath, Guid RestoreId, bool RestartRequired);

internal sealed record RestoreTicketSnapshot(
    [property: JsonPropertyName("local_ticket_id")] string LocalTicketId,
    [property: JsonPropertyName("display_ticket_number")] int? DisplayTicketNumber,
    [property: JsonPropertyName("ticket_date")] string? TicketDate,
    [property: JsonPropertyName("customer_name")] string? CustomerName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("checked_in_at")] string CheckedInAt,
    [property: JsonPropertyName("barber_id")] string? BarberId,
    [property: JsonPropertyName("appointment_id")] string? AppointmentId,
    [property: JsonPropertyName("started_at")] string? StartedAt,
    [property: JsonPropertyName("completed_at")] string? CompletedAt,
    [property: JsonPropertyName("cancelled_at")] string? CancelledAt);

internal sealed record RestoreTicketItemSnapshot(
    [property: JsonPropertyName("local_item_id")] string LocalItemId,
    [property: JsonPropertyName("local_ticket_id")] string LocalTicketId,
    [property: JsonPropertyName("service_id")] string ServiceId,
    [property: JsonPropertyName("price_cents")] long PriceCents);

internal sealed record RestorePaymentSnapshot(
    [property: JsonPropertyName("local_payment_id")] string LocalPaymentId,
    [property: JsonPropertyName("local_ticket_id")] string LocalTicketId,
    [property: JsonPropertyName("payment_method")] string PaymentMethod,
    [property: JsonPropertyName("amount_cents")] long AmountCents,
    [property: JsonPropertyName("receipt_number")] string? ReceiptNumber,
    [property: JsonPropertyName("payment_reference")] string? PaymentReference,
    [property: JsonPropertyName("collected_at")] string CollectedAt);

internal sealed class DesktopBackupPasswordRequiredException : Exception
{
    public DesktopBackupPasswordRequiredException(string message)
        : base(message)
    {
    }
}
