using System.Net.Http.Json;
using System.Text.Json;

namespace Barberia.Desktop.Services;

internal abstract class LanClientBase
{
    private readonly StationSettings _settings;

    protected LanClientBase(StationSettings settings)
    {
        _settings = settings;
    }

    protected T Get<T>(string path)
    {
        return Send<T>(HttpMethod.Get, path, body: null);
    }

    protected T Post<T>(string path, object? body = null)
    {
        return Send<T>(HttpMethod.Post, path, body);
    }

    protected void Post(string path, object? body = null)
    {
        _ = Send<LanCommandResult>(HttpMethod.Post, path, body);
    }

    private T Send<T>(HttpMethod method, string path, object? body)
    {
        try
        {
            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri(_settings.LanServerUrl),
                Timeout = TimeSpan.FromSeconds(10)
            };
            using var request = new HttpRequestMessage(method, path);
            request.Headers.TryAddWithoutValidation(LanApiContract.DeviceIdHeader, _settings.DeviceId);
            if (!string.IsNullOrWhiteSpace(_settings.LanSharedSecret))
            {
                request.Headers.TryAddWithoutValidation(LanApiContract.DeviceSecretHeader, _settings.LanSharedSecret);
            }

            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            using var response = httpClient.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = response.Content.ReadFromJsonAsync<LanApiError>().GetAwaiter().GetResult();
                throw new InvalidOperationException(error?.Message ?? $"LAN request failed: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            try
            {
                var result = response.Content.ReadFromJsonAsync<T>().GetAwaiter().GetResult();
                return result ?? throw new InvalidOperationException("LAN host returned an empty response.");
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    $"LAN response from {_settings.LanServerUrl}{path} could not be parsed as {typeof(T).FullName}: {exception.Message}",
                    exception);
            }
            catch (NotSupportedException exception)
            {
                throw new InvalidOperationException(
                    $"LAN response type {typeof(T).FullName} is not supported for JSON parsing from {_settings.LanServerUrl}{path}: {exception.Message}",
                    exception);
            }
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException($"Could not reach PC3 at {_settings.LanServerUrl}: {exception.Message}", exception);
        }
        catch (TaskCanceledException exception)
        {
            throw new InvalidOperationException($"PC3 did not respond before the LAN timeout: {exception.Message}", exception);
        }
    }
}
