namespace Barberia.Data.Models;

public sealed record DailyRotationEntry(
    DateOnly BusinessDate,
    Guid BarberId,
    int QueuePosition,
    DateTimeOffset ArrivedAt,
    DateTimeOffset UpdatedAt);
