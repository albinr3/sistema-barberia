namespace Barberia.ApiClient.Sync;

public sealed record CloudSyncEnvelope(
    Guid Id,
    DateTimeOffset OccurredAt,
    string EventType,
    string AggregateType,
    Guid AggregateId,
    string Payload,
    string? DeviceId);
