namespace Barberia.ApiClient.Sync;

public sealed class UnavailableCloudSyncClient : ICloudSyncClient
{
    public Task<CloudSyncResult> PushAsync(CloudSyncEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return Task.FromResult(CloudSyncResult.Failure("Cloud sync client is not configured for Phase 1."));
    }
}
