using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Barberia.ApiClient.Sync;

public sealed class HttpCloudSyncClient : ICloudSyncClient
{
    private readonly HttpClient _httpClient;
    private readonly string _deviceId;
    private readonly string _deviceSecret;

    public HttpCloudSyncClient(HttpClient httpClient, string deviceId, string deviceSecret)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        _deviceSecret = deviceSecret ?? throw new ArgumentNullException(nameof(deviceSecret));
    }

    public async Task<CloudSyncResult> PushAsync(CloudSyncEnvelope envelope)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "functions/v1/sync-events");
            request.Headers.Add("x-device-id", _deviceId);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _deviceSecret);

            var payload = new
            {
                events = new[]
                {
                    new
                    {
                        source_event_id = envelope.Id,
                        schema_version = "1.0",
                        occurred_at = envelope.OccurredAt,
                        event_type = envelope.EventType,
                        aggregate_type = envelope.AggregateType,
                        aggregate_id = envelope.AggregateId,
                        payload = JsonDocument.Parse(envelope.Payload).RootElement
                    }
                }
            };

            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return CloudSyncResult.Failure($"HTTP {response.StatusCode}: {responseContent}");
            }

            return ParseSyncResponse(responseContent, envelope.Id)
                ?? CloudSyncResult.Failure("Cloud sync response did not include a result for the pushed event.");
        }
        catch (Exception ex)
        {
            return CloudSyncResult.Failure(ex.Message);
        }
    }

    private static CloudSyncResult? ParseSyncResponse(string responseContent, Guid sourceEventId)
    {
        using var document = JsonDocument.Parse(responseContent);
        if (!document.RootElement.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var result in results.EnumerateArray())
        {
            if (!result.TryGetProperty("source_event_id", out var responseEventId)
                || !string.Equals(responseEventId.GetString(), sourceEventId.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!result.TryGetProperty("status", out var statusElement))
            {
                return CloudSyncResult.Failure("Cloud sync response did not include a status for the pushed event.");
            }

            var status = statusElement.GetString();
            if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
            {
                return CloudSyncResult.Success();
            }

            var message = result.TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : null;
            return CloudSyncResult.Failure(string.IsNullOrWhiteSpace(message)
                ? "Cloud sync event failed."
                : message);
        }

        return null;
    }
}
