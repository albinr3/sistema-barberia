namespace Barberia.Data.Models;

public sealed record AuditEvent(
    Guid Id,
    DateTimeOffset OccurredAt,
    string EventType,
    string AggregateType,
    Guid AggregateId,
    string Payload,
    string? DeviceId = null);
