namespace Barberia.ApiClient.Sync;

public sealed record CloudSyncResult(bool Succeeded, string? ErrorMessage)
{
    public static CloudSyncResult Success()
    {
        return new CloudSyncResult(true, null);
    }

    public static CloudSyncResult Failure(string errorMessage)
    {
        return new CloudSyncResult(false, string.IsNullOrWhiteSpace(errorMessage)
            ? "Cloud sync failed."
            : errorMessage.Trim());
    }
}
