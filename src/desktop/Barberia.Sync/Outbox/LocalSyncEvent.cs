namespace Barberia.Sync.Outbox;

public sealed class LocalSyncEvent
{
    public LocalSyncEvent(
        Guid id,
        DateTimeOffset occurredAt,
        string eventType,
        string aggregateType,
        Guid aggregateId,
        string payload,
        string? deviceId = null)
    {
        if (id == Guid.Empty || aggregateId == Guid.Empty)
        {
            throw new ArgumentException("Sync event and aggregate ids are required.");
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("Sync event type is required.", nameof(eventType));
        }

        if (string.IsNullOrWhiteSpace(aggregateType))
        {
            throw new ArgumentException("Sync aggregate type is required.", nameof(aggregateType));
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Sync event payload is required.", nameof(payload));
        }

        Id = id;
        OccurredAt = occurredAt;
        EventType = eventType.Trim();
        AggregateType = aggregateType.Trim();
        AggregateId = aggregateId;
        Payload = payload;
        DeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
    }

    public Guid Id { get; }

    public DateTimeOffset OccurredAt { get; }

    public string EventType { get; }

    public string AggregateType { get; }

    public Guid AggregateId { get; }

    public string Payload { get; }

    public string? DeviceId { get; }
}
