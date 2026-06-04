namespace Barberia.Sync.Outbox;

public sealed record SyncDispatchResult(int Attempted, int Synced, int Failed);
