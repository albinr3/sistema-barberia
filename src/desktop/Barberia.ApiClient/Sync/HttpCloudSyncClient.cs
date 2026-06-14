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

            if (response.IsSuccessStatusCode)
            {
                return CloudSyncResult.Success();
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return CloudSyncResult.Failure($"HTTP {response.StatusCode}: {errorContent}");
        }
        catch (Exception ex)
        {
            return CloudSyncResult.Failure(ex.Message);
        }
    }
}
