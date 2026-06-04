namespace Barberia.ApiClient.Sync;

public interface ICloudSyncClient
{
    CloudSyncResult Push(CloudSyncEnvelope envelope);
}
