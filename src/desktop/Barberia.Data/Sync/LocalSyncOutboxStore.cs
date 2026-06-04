namespace Barberia.Data.Sync;

public sealed class LocalSyncOutboxStore
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public LocalSyncOutboxStore(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public SyncOutboxEvent Add(SyncOutboxEvent outboxEvent)
    {
        using var connection = _connectionFactory.OpenConnection();
        new SyncOutboxRepository(connection).Add(outboxEvent);

        return outboxEvent;
    }

    public IReadOnlyList<SyncOutboxEvent> ListReadyToSync(DateTimeOffset now, int limit)
    {
        using var connection = _connectionFactory.OpenConnection();
        return new SyncOutboxRepository(connection).ListReadyToSync(now, limit);
    }

    public void MarkSynced(Guid id, DateTimeOffset syncedAt)
    {
        using var connection = _connectionFactory.OpenConnection();
        new SyncOutboxRepository(connection).MarkSynced(id, syncedAt);
    }

    public void MarkAttemptFailed(Guid id, DateTimeOffset attemptedAt, DateTimeOffset nextAttemptAt, string errorMessage)
    {
        using var connection = _connectionFactory.OpenConnection();
        new SyncOutboxRepository(connection).MarkAttemptFailed(id, attemptedAt, nextAttemptAt, errorMessage);
    }
}
