using Barberia.Data.Sync;

namespace Barberia.Sync.Outbox;

public sealed class SyncOutboxRecorder
{
    private readonly SyncOutboxRepository? _repository;
    private readonly LocalSyncOutboxStore? _store;

    public SyncOutboxRecorder(SyncOutboxRepository repository)
    {
        _repository = repository;
    }

    public SyncOutboxRecorder(LocalSyncOutboxStore store)
    {
        _store = store;
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

        if (_repository is not null)
        {
            _repository.Add(outboxEvent);
            return outboxEvent;
        }

        return _store?.Add(outboxEvent)
            ?? throw new InvalidOperationException("Sync outbox recorder is not configured.");
    }
}
