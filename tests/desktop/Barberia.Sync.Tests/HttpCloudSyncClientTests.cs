using System.Net;
using System.Net.Http;
using System.Text;
using Barberia.ApiClient.Sync;
using Xunit;

namespace Barberia.Sync.Tests;

public sealed class HttpCloudSyncClientTests
{
    [Fact]
    public async Task PushAsync_ReturnsSuccess_WhenEventResultIsSuccessful()
    {
        var envelope = CreateEnvelope();
        using var httpClient = CreateHttpClient("""
            {
              "results": [
                {
                  "source_event_id": "%SOURCE_EVENT_ID%",
                  "status": "success"
                }
              ]
            }
            """.Replace("%SOURCE_EVENT_ID%", envelope.Id.ToString(), StringComparison.Ordinal));
        var client = new HttpCloudSyncClient(httpClient, "device-1", "secret");

        var result = await client.PushAsync(envelope);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task PushAsync_ReturnsFailure_WhenEventResultContainsError()
    {
        var envelope = CreateEnvelope();
        using var httpClient = CreateHttpClient("""
            {
              "results": [
                {
                  "source_event_id": "%SOURCE_EVENT_ID%",
                  "status": "error",
                  "message": "Restore ticket revert failed: missing column restore_reverted_at"
                }
              ]
            }
            """.Replace("%SOURCE_EVENT_ID%", envelope.Id.ToString(), StringComparison.Ordinal));
        var client = new HttpCloudSyncClient(httpClient, "device-1", "secret");

        var result = await client.PushAsync(envelope);

        Assert.False(result.Succeeded);
        Assert.Equal("Restore ticket revert failed: missing column restore_reverted_at", result.ErrorMessage);
    }

    [Fact]
    public async Task PushAsync_ReturnsFailure_WhenResponseOmitsMatchingEventResult()
    {
        using var httpClient = CreateHttpClient("""
            {
              "results": [
                {
                  "source_event_id": "11111111-1111-1111-1111-111111111111",
                  "status": "success"
                }
              ]
            }
            """);
        var client = new HttpCloudSyncClient(httpClient, "device-1", "secret");

        var result = await client.PushAsync(CreateEnvelope());

        Assert.False(result.Succeeded);
        Assert.Equal("Cloud sync response did not include a result for the pushed event.", result.ErrorMessage);
    }

    private static CloudSyncEnvelope CreateEnvelope()
    {
        return new CloudSyncEnvelope(
            Guid.NewGuid(),
            DateTimeOffset.Parse("2026-06-18T10:00:00-04:00"),
            "desktop.restore_applied",
            "desktop_restore",
            Guid.NewGuid(),
            """{"restore_id":"22222222-2222-2222-2222-222222222222"}""",
            "device-1");
    }

    private static HttpClient CreateHttpClient(string responseContent)
    {
        return new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent, Encoding.UTF8, "application/json"),
        }))
        {
            BaseAddress = new Uri("https://example.supabase.co/")
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
