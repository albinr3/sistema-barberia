namespace Barberia.ApiClient.Sync;

public sealed class UnavailableCloudSyncClient : ICloudSyncClient
{
    public CloudSyncResult Push(CloudSyncEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return CloudSyncResult.Failure("Cloud sync client is not configured for Phase 1.");
    }
}
