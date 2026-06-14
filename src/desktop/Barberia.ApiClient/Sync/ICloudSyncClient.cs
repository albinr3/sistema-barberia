namespace Barberia.ApiClient.Sync;

public interface ICloudSyncClient
{
    Task<CloudSyncResult> PushAsync(CloudSyncEnvelope envelope);
}
