using System.Text.Json;

namespace Barberia.Desktop.Services;

internal sealed record DesktopSyncSettings(
    string SupabaseUrl,
    string DeviceId,
    string DeviceSecret,
    int PollSeconds)
{
    public static DesktopSyncSettings? Load()
    {
        var path = Path.Combine(LocalAppPaths.ConfigDirectory, "sync-settings.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<DesktopSyncSettings>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (settings is null
                || string.IsNullOrWhiteSpace(settings.SupabaseUrl)
                || string.IsNullOrWhiteSpace(settings.DeviceId)
                || string.IsNullOrWhiteSpace(settings.DeviceSecret))
            {
                return null;
            }

            return settings with { PollSeconds = Math.Max(settings.PollSeconds, 30) };
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
