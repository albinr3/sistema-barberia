namespace Barberia.Desktop.Services;

internal static class StationRuntime
{
    public static StationSettings Current { get; private set; } = new(
        StationRole.Development,
        StationSettings.DefaultLanServerUrl,
        StationSettings.DefaultLanListenUrl,
        Environment.MachineName,
        null,
        StartLanHostInDevelopment: false);

    public static void Configure(StationSettings settings)
    {
        Current = settings;
    }
}
