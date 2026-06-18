using Barberia.Data;
using Barberia.Data.Sync;
using Barberia.Desktop.Services;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using Xunit;

namespace Barberia.Desktop.Tests;

public sealed class DesktopBackupServiceRestoreTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly string _databasePath;

    public DesktopBackupServiceRestoreTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "barberia-restore-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDirectory);
        _databasePath = Path.Combine(_rootDirectory, "barberia-local.db");
    }

    [Fact]
    public async Task RestoreBackupAsync_WithValidUnprotectedZip_ReplacesCurrentDatabase()
    {
        CreateDatabase(_databasePath, "Current");
        var backupDatabasePath = Path.Combine(_rootDirectory, "backup.db");
        CreateDatabase(backupDatabasePath, "Restored");
        var zipPath = CreateBackupZip(backupDatabasePath, "backup.zip");
        var service = CreateService();

        var result = await service.RestoreBackupAsync(zipPath);

        Assert.True(result.RestartRequired);
        Assert.True(File.Exists(result.SafetyBackupPath));
        Assert.Equal("Restored", ReadSingleBarberName(_databasePath));
    }

    [Fact]
    public async Task RestoreBackupAsync_WithProtectedZipAndCorrectPassword_ReplacesCurrentDatabase()
    {
        CreateDatabase(_databasePath, "Current");
        var backupDatabasePath = Path.Combine(_rootDirectory, "protected.db");
        CreateDatabase(backupDatabasePath, "Protected");
        var zipPath = CreateBackupZip(backupDatabasePath, "protected.zip", "secret");
        var service = CreateService();

        await service.RestoreBackupAsync(zipPath, "secret");

        Assert.Equal("Protected", ReadSingleBarberName(_databasePath));
    }

    [Fact]
    public async Task RestoreBackupAsync_WithWrongPassword_DoesNotReplaceCurrentDatabase()
    {
        CreateDatabase(_databasePath, "Current");
        var backupDatabasePath = Path.Combine(_rootDirectory, "protected.db");
        CreateDatabase(backupDatabasePath, "Protected");
        var zipPath = CreateBackupZip(backupDatabasePath, "protected.zip", "secret");
        var service = CreateService();

        await Assert.ThrowsAsync<DesktopBackupPasswordRequiredException>(
            () => service.RestoreBackupAsync(zipPath, "wrong"));

        Assert.Equal("Current", ReadSingleBarberName(_databasePath));
    }

    [Fact]
    public async Task RestoreBackupAsync_WithInvalidZipDatabase_DoesNotReplaceCurrentDatabase()
    {
        CreateDatabase(_databasePath, "Current");
        var invalidDatabasePath = Path.Combine(_rootDirectory, "invalid.db");
        File.WriteAllText(invalidDatabasePath, "not sqlite");
        var zipPath = CreateBackupZip(invalidDatabasePath, "invalid.zip");
        var service = CreateService();

        await Assert.ThrowsAsync<SqliteException>(() => service.RestoreBackupAsync(zipPath));

        Assert.Equal("Current", ReadSingleBarberName(_databasePath));
    }

    [Fact]
    public async Task RestoreBackupAsync_WithPendingSyncEvents_BlocksRestore()
    {
        CreateDatabase(_databasePath, "Current", hasPendingSyncEvent: true);
        var backupDatabasePath = Path.Combine(_rootDirectory, "backup.db");
        CreateDatabase(backupDatabasePath, "Restored");
        var zipPath = CreateBackupZip(backupDatabasePath, "backup.zip");
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RestoreBackupAsync(zipPath));

        Assert.Contains("pending sync event", exception.Message);
        Assert.Equal("Current", ReadSingleBarberName(_databasePath));
    }

    [Fact]
    public async Task RestoreBackupAsync_CreatesPreRestoreSafetyBackup()
    {
        CreateDatabase(_databasePath, "Current");
        var backupDatabasePath = Path.Combine(_rootDirectory, "backup.db");
        CreateDatabase(backupDatabasePath, "Restored");
        var zipPath = CreateBackupZip(backupDatabasePath, "backup.zip");
        var service = CreateService();

        var result = await service.RestoreBackupAsync(zipPath);

        Assert.StartsWith(Path.Combine(_rootDirectory, "backups", "pre-restore_"), result.SafetyBackupPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(result.SafetyBackupPath));
        Assert.Equal("Current", ReadSingleBarberNameFromZip(result.SafetyBackupPath));
    }

    [Fact]
    public async Task RestoreBackupAsync_EnqueuesRestoreAppliedEventInRestoredDatabase()
    {
        CreateDatabase(_databasePath, "Current");
        var backupDatabasePath = Path.Combine(_rootDirectory, "backup.db");
        CreateDatabase(backupDatabasePath, "Restored");
        var zipPath = CreateBackupZip(backupDatabasePath, "backup.zip");
        var service = CreateService();

        var result = await service.RestoreBackupAsync(zipPath);

        using var connection = CreateConnectionFactory(_databasePath).OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT aggregate_id, payload
            FROM sync_outbox_events
            WHERE event_type = 'desktop.restore_applied';
            """;
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(result.RestoreId.ToString(), reader.GetString(0));

        using var document = JsonDocument.Parse(reader.GetString(1));
        var root = document.RootElement;
        Assert.Equal(result.RestoreId.ToString(), root.GetProperty("restore_id").GetString());
        var snapshot = root.GetProperty("snapshot");
        Assert.Single(snapshot.GetProperty("tickets").EnumerateArray());
        Assert.Single(snapshot.GetProperty("payments").EnumerateArray());
        Assert.Single(snapshot.GetProperty("ticket_items").EnumerateArray());
        Assert.False(reader.Read());
    }

    [Fact]
    public async Task RestoreBackupAsync_WithPendingOutboxInsideBackup_BlocksRestore()
    {
        CreateDatabase(_databasePath, "Current");
        var backupDatabasePath = Path.Combine(_rootDirectory, "backup-with-pending.db");
        CreateDatabase(backupDatabasePath, "Restored", hasPendingSyncEvent: true);
        var zipPath = CreateBackupZip(backupDatabasePath, "backup-with-pending.zip");
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RestoreBackupAsync(zipPath));

        Assert.Contains("restored backup contains", exception.Message);
        Assert.Equal("Current", ReadSingleBarberName(_databasePath));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private DesktopBackupService CreateService()
    {
        return new DesktopBackupService(
            _rootDirectory,
            _databasePath,
            () => CreateConnectionFactory(_databasePath),
            () => new DesktopSyncSettings(
                "https://example.supabase.co",
                "11111111-1111-1111-1111-111111111111",
                "test-secret",
                60));
    }

    private static void CreateDatabase(string path, string barberName, bool hasPendingSyncEvent = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var connectionFactory = CreateConnectionFactory(path);
        using (var connection = connectionFactory.OpenConnection())
        {
            LocalDatabaseInitializer.Initialize(connection);

            var now = DateTimeOffset.Parse("2026-06-18T12:00:00-04:00");
            var barberId = Guid.NewGuid();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO barbers (
                        id, display_name, state, clients_served_today, rotation_order, station_number,
                        checked_in_at, profile_image_path, is_active, commission_percentage, updated_at
                    ) VALUES (
                        $id, $display_name, 0, 0, 0, 1, NULL, NULL, 1, 65, $updated_at
                    );
                    """;
                command.Parameters.AddWithValue("$id", barberId.ToString());
                command.Parameters.AddWithValue("$display_name", barberName);
                command.Parameters.AddWithValue("$updated_at", now.ToString("O"));
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                var turnId = Guid.NewGuid();
                var serviceId = Guid.NewGuid();
                var paymentId = Guid.NewGuid();
                command.CommandText = """
                    INSERT INTO services (
                        id, name, price_cents, is_active, display_order, created_at, updated_at
                    ) VALUES (
                        $service_id, 'Regular Cut', 3000, 1, 0, $created_at, $updated_at
                    );

                    INSERT INTO turns (
                        id, ticket_number, display_ticket_number, ticket_date, state, source, customer_name,
                        checked_in_at, assigned_barber_id, appointment_id, requested_barber_ids,
                        started_at, completed_at, cancelled_at, updated_at
                    ) VALUES (
                        $turn_id, 'W-001', 1, '2026-06-18', 4, 0, $customer_name,
                        $checked_in_at, NULL, NULL, NULL, $started_at, $completed_at, NULL, $updated_at
                    );

                    INSERT INTO cash_payments (
                        id, turn_id, barber_id, service_id, amount_cents, currency, collected_at,
                        device_id, receipt_number, cash_drawer_opened, commission_cents,
                        service_price_cents, additional_cents, payment_method, payment_reference
                    ) VALUES (
                        $payment_id, $turn_id, $barber_id, $service_id, 3000, 'USD', $collected_at,
                        'test-device', 'R-001', 1, 1950, 3000, 0, 0, NULL
                    );
                    """;
                command.Parameters.AddWithValue("$service_id", serviceId.ToString());
                command.Parameters.AddWithValue("$turn_id", turnId.ToString());
                command.Parameters.AddWithValue("$payment_id", paymentId.ToString());
                command.Parameters.AddWithValue("$barber_id", barberId.ToString());
                command.Parameters.AddWithValue("$customer_name", $"{barberName} Customer");
                command.Parameters.AddWithValue("$created_at", now.ToString("O"));
                command.Parameters.AddWithValue("$updated_at", now.ToString("O"));
                command.Parameters.AddWithValue("$checked_in_at", now.ToString("O"));
                command.Parameters.AddWithValue("$started_at", now.ToString("O"));
                command.Parameters.AddWithValue("$completed_at", now.ToString("O"));
                command.Parameters.AddWithValue("$collected_at", now.ToString("O"));
                command.ExecuteNonQuery();
            }

            if (hasPendingSyncEvent)
            {
                new SyncOutboxRepository(connection).Add(new SyncOutboxEvent(
                    Guid.NewGuid(),
                    now,
                    "test.event",
                    "test",
                    Guid.NewGuid(),
                    "{}",
                    "test-device",
                    now,
                    SyncOutboxEventState.Pending,
                    0,
                    null,
                    null,
                    null,
                    null));
            }
        }

        SqliteConnection.ClearAllPools();
    }

    private string CreateBackupZip(string databasePath, string zipFileName, string? password = null)
    {
        var zipPath = Path.Combine(_rootDirectory, zipFileName);
        using var output = File.Create(zipPath);
        using var zipStream = new ZipOutputStream(output);
        zipStream.SetLevel(9);
        if (!string.IsNullOrEmpty(password))
        {
            zipStream.Password = password;
        }

        var entry = new ZipEntry(Path.GetFileName(databasePath));
        zipStream.PutNextEntry(entry);
        using var input = File.OpenRead(databasePath);
        input.CopyTo(zipStream);
        zipStream.CloseEntry();
        return zipPath;
    }

    private static string ReadSingleBarberName(string databasePath)
    {
        using (var connection = CreateConnectionFactory(databasePath).OpenConnection())
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT display_name FROM barbers ORDER BY display_name LIMIT 1;";
            return Assert.IsType<string>(command.ExecuteScalar());
        }
    }

    private string ReadSingleBarberNameFromZip(string zipPath)
    {
        var extractPath = Path.Combine(_rootDirectory, $"{Guid.NewGuid():N}.db");
        using (var zipFile = new ZipFile(zipPath))
        {
            var entry = zipFile.Cast<ZipEntry>().Single(zipEntry => zipEntry.IsFile && zipEntry.Name.EndsWith(".db", StringComparison.OrdinalIgnoreCase));
            using var input = zipFile.GetInputStream(entry);
            using var output = File.Create(extractPath);
            input.CopyTo(output);
        }

        return ReadSingleBarberName(extractPath);
    }

    private static SqliteConnectionFactory CreateConnectionFactory(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        };

        return new SqliteConnectionFactory(builder.ToString());
    }
}
