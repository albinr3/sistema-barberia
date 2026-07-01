namespace Barberia.Desktop.Services;

internal static class LanApiContract
{
    public const string Version = "1";
    public const string DeviceIdHeader = "X-Barberia-Device-Id";
    public const string DeviceSecretHeader = "X-Barberia-Device-Secret";
}

internal sealed record LanHealthResponse(
    string ApiVersion,
    string AppVersion,
    string MachineName,
    string StationRole,
    DateTimeOffset ServerTime);

internal sealed record LanApiError(string Message);

internal sealed record LanCommandResult(string Message = "OK");
