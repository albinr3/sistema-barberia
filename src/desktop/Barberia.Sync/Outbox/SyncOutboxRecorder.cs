using Barberia.Data.Sync;

namespace Barberia.Sync.Outbox;

public sealed class SyncOutboxRecorder
{
    private readonly LocalSyncOutboxStore _outboxStore;

    public SyncOutboxRecorder(LocalSyncOutboxStore outboxStore)
    {
        _outboxStore = outboxStore;
    }

    public SyncOutboxEvent Enqueue(LocalSyncEvent syncEvent, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        var outboxEvent = SyncOutboxEvent.Pending(
            syncEvent.Id,
            syncEvent.OccurredAt,
            syncEvent.EventType,
            syncEvent.AggregateType,
            syncEvent.AggregateId,
            syncEvent.Payload,
            syncEvent.DeviceId,
            createdAt);

        return _outboxStore.Add(outboxEvent);
    }
}
