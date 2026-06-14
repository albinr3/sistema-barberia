using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Barberia.ApiClient.Sync;

public sealed class CloudPullService
{
    private readonly HttpClient _httpClient;
    private readonly string _deviceId;
    private readonly string _deviceSecret;

    public CloudPullService(HttpClient httpClient, string deviceId, string deviceSecret)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        _deviceSecret = deviceSecret ?? throw new ArgumentNullException(nameof(deviceSecret));
    }

    public async Task<string?> PullChangesAsync(string cursor)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "functions/v1/sync-changes");
            request.Headers.Add("x-device-id", _deviceId);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _deviceSecret);

            var payload = new { cursor = cursor };
            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content; // Contains new_cursor and changes
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
