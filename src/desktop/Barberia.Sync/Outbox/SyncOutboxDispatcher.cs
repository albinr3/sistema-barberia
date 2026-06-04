using Barberia.ApiClient.Sync;
using Barberia.Data.Sync;

namespace Barberia.Sync.Outbox;

public sealed class SyncOutboxDispatcher
{
    private readonly LocalSyncOutboxStore _outboxStore;
    private readonly ICloudSyncClient _cloudSyncClient;
    private readonly SyncRetryPolicy _retryPolicy;
    private readonly int _batchSize;

    public SyncOutboxDispatcher(
        LocalSyncOutboxStore outboxStore,
        ICloudSyncClient cloudSyncClient,
        SyncRetryPolicy? retryPolicy = null,
        int batchSize = 25)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");
        }

        _outboxStore = outboxStore;
        _cloudSyncClient = cloudSyncClient;
        _retryPolicy = retryPolicy ?? SyncRetryPolicy.Default;
        _batchSize = batchSize;
    }

    public SyncDispatchResult DispatchDue(DateTimeOffset now)
    {
        var dueEvents = _outboxStore.ListReadyToSync(now, _batchSize);
        var synced = 0;
        var failed = 0;

        foreach (var outboxEvent in dueEvents)
        {
            var pushResult = PushSafely(outboxEvent);
            if (pushResult.Succeeded)
            {
                _outboxStore.MarkSynced(outboxEvent.Id, now);
                synced++;
                continue;
            }

            var nextAttemptCount = outboxEvent.AttemptCount + 1;
            _outboxStore.MarkAttemptFailed(
                outboxEvent.Id,
                now,
                _retryPolicy.GetNextAttemptAt(now, nextAttemptCount),
                pushResult.ErrorMessage ?? "Cloud sync failed.");
            failed++;
        }

        return new SyncDispatchResult(dueEvents.Count, synced, failed);
    }

    private CloudSyncResult PushSafely(SyncOutboxEvent outboxEvent)
    {
        try
        {
            return _cloudSyncClient.Push(ToCloudEnvelope(outboxEvent));
        }
        catch (Exception ex)
        {
            return CloudSyncResult.Failure(ex.Message);
        }
    }

    private static CloudSyncEnvelope ToCloudEnvelope(SyncOutboxEvent outboxEvent)
    {
        return new CloudSyncEnvelope(
            outboxEvent.Id,
            outboxEvent.OccurredAt,
            outboxEvent.EventType,
            outboxEvent.AggregateType,
            outboxEvent.AggregateId,
            outboxEvent.Payload,
            outboxEvent.DeviceId);
    }
}
