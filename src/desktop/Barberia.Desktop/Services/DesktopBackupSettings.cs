using System.Text.Json;

namespace Barberia.Desktop.Services;

internal sealed record DesktopBackupSettings(
    bool Enabled,
    string TimeOfDay, // "HH:mm" format, default "20:00"
    string? EncryptedPassword, // Base64 encoded DPAPI protected string
    int LocalRetentionDays,
    int CloudRetentionDays)
{
    public static DesktopBackupSettings Load()
    {
        var path = Path.Combine(LocalAppPaths.ConfigDirectory, "backup-settings.json");
        if (!File.Exists(path))
        {
            return new DesktopBackupSettings(true, "20:00", null, 7, 7);
        }

        try
        {
            var settings = JsonSerializer.Deserialize<DesktopBackupSettings>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (settings is null)
            {
                return new DesktopBackupSettings(true, "20:00", null, 7, 7);
            }

            return settings with 
            { 
                LocalRetentionDays = Math.Max(settings.LocalRetentionDays, 1),
                CloudRetentionDays = Math.Max(settings.CloudRetentionDays, 1)
            };
        }
        catch (JsonException)
        {
            return new DesktopBackupSettings(true, "20:00", null, 7, 7);
        }
        catch (IOException)
        {
            return new DesktopBackupSettings(true, "20:00", null, 7, 7);
        }
    }

    public void Save()
    {
        var path = Path.Combine(LocalAppPaths.ConfigDirectory, "backup-settings.json");
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
