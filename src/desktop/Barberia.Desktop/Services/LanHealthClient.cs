using System.Net.Http.Json;

namespace Barberia.Desktop.Services;

internal sealed class LanHealthClient
{
    private readonly StationSettings _settings;

    public LanHealthClient(StationSettings settings)
    {
        _settings = settings;
    }

    public async Task<LanHealthCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri(_settings.LanServerUrl),
                Timeout = TimeSpan.FromSeconds(3)
            };
            using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
            request.Headers.TryAddWithoutValidation(LanApiContract.DeviceIdHeader, _settings.DeviceId);
            if (!string.IsNullOrWhiteSpace(_settings.LanSharedSecret))
            {
                request.Headers.TryAddWithoutValidation(LanApiContract.DeviceSecretHeader, _settings.LanSharedSecret);
            }

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return LanHealthCheckResult.Unavailable($"PC3 rejected the LAN request: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var health = await response.Content.ReadFromJsonAsync<LanHealthResponse>(cancellationToken);
            if (health is null)
            {
                return LanHealthCheckResult.Unavailable("PC3 returned an empty health response.");
            }

            if (!string.Equals(health.ApiVersion, LanApiContract.Version, StringComparison.Ordinal))
            {
                return LanHealthCheckResult.VersionMismatch(health.ApiVersion);
            }

            return LanHealthCheckResult.Available(health);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return LanHealthCheckResult.Unavailable($"Could not reach PC3 at {_settings.LanServerUrl}: {exception.Message}");
        }
    }
}

internal sealed record LanHealthCheckResult(
    bool IsAvailable,
    bool IsVersionMismatch,
    string Message,
    LanHealthResponse? Health)
{
    public static LanHealthCheckResult Available(LanHealthResponse health)
    {
        return new LanHealthCheckResult(true, false, "PC3 LAN host is available.", health);
    }

    public static LanHealthCheckResult Unavailable(string message)
    {
        return new LanHealthCheckResult(false, false, message, null);
    }

    public static LanHealthCheckResult VersionMismatch(string actualVersion)
    {
        return new LanHealthCheckResult(
            false,
            true,
            $"PC3 LAN API version is {actualVersion}; this station requires {LanApiContract.Version}. Update all stations to the same release.",
            null);
    }
}
