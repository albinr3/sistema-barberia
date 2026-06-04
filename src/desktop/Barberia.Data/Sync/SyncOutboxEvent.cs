namespace Barberia.Data.Sync;

public sealed record SyncOutboxEvent(
    Guid Id,
    DateTimeOffset OccurredAt,
    string EventType,
    string AggregateType,
    Guid AggregateId,
    string Payload,
    string? DeviceId,
    DateTimeOffset CreatedAt,
    SyncOutboxEventState State,
    int AttemptCount,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? LastAttemptedAt,
    DateTimeOffset? SyncedAt,
    string? LastError)
{
    public static SyncOutboxEvent Pending(
        Guid id,
        DateTimeOffset occurredAt,
        string eventType,
        string aggregateType,
        Guid aggregateId,
        string payload,
        string? deviceId,
        DateTimeOffset createdAt)
    {
        return new SyncOutboxEvent(
            id,
            occurredAt,
            eventType.Trim(),
            aggregateType.Trim(),
            aggregateId,
            payload,
            string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim(),
            createdAt,
            SyncOutboxEventState.Pending,
            0,
            createdAt,
            null,
            null,
            null);
    }
}
