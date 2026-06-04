namespace Barberia.Sync.Outbox;

public sealed class SyncRetryPolicy
{
    public static SyncRetryPolicy Default { get; } = new(
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(30));

    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _maxDelay;

    public SyncRetryPolicy(TimeSpan initialDelay, TimeSpan maxDelay)
    {
        if (initialDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(initialDelay), "Initial retry delay must be greater than zero.");
        }

        if (maxDelay < initialDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDelay), "Max retry delay must be greater than or equal to the initial delay.");
        }

        _initialDelay = initialDelay;
        _maxDelay = maxDelay;
    }

    public DateTimeOffset GetNextAttemptAt(DateTimeOffset now, int nextAttemptCount)
    {
        if (nextAttemptCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextAttemptCount), "Next attempt count must be greater than zero.");
        }

        var delayTicks = _initialDelay.Ticks;
        for (var attempt = 1; attempt < nextAttemptCount; attempt++)
        {
            if (delayTicks >= _maxDelay.Ticks / 2)
            {
                delayTicks = _maxDelay.Ticks;
                break;
            }

            delayTicks *= 2;
        }

        if (delayTicks > _maxDelay.Ticks)
        {
            delayTicks = _maxDelay.Ticks;
        }

        return now.Add(TimeSpan.FromTicks(delayTicks));
    }
}
