using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Barberia.Desktop.Services;

internal sealed record StationSettings(
    StationRole Role,
    string LanServerUrl,
    string LanListenUrl,
    string DeviceId,
    string? LanSharedSecret,
    bool StartLanHostInDevelopment)
{
    public const string FileName = "station-settings.json";
    public const string DefaultLanServerUrl = "http://localhost:5128";
    public const string DefaultLanListenUrl = "http://0.0.0.0:5128";

    public static StationSettings Load(string[] commandLineArgs)
    {
        var configured = LoadFromFile();
        var role = TryParseRoleArgument(commandLineArgs)
            ?? configured?.Role
            ?? InferRoleFromProcessName()
            ?? StationRole.Development;

        return new StationSettings(
            role,
            NormalizeBaseUrl(configured?.LanServerUrl, DefaultLanServerUrl),
            NormalizeBaseUrl(configured?.LanListenUrl, DefaultLanListenUrl),
            string.IsNullOrWhiteSpace(configured?.DeviceId) ? Environment.MachineName : configured.DeviceId,
            string.IsNullOrWhiteSpace(configured?.LanSharedSecret) ? null : configured.LanSharedSecret,
            configured?.StartLanHostInDevelopment ?? false);
    }

    internal static StationRole? TryParseRoleArgument(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--station=", StringComparison.OrdinalIgnoreCase))
            {
                return ParseRoleOrNull(arg["--station=".Length..]);
            }

            if (string.Equals(arg, "--station", StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Count)
            {
                return ParseRoleOrNull(args[i + 1]);
            }
        }

        return null;
    }

    internal static StationRole? InferRoleFromProcessName()
    {
        var processName = Process.GetCurrentProcess().ProcessName;
        if (processName.Contains("KioskRotation", StringComparison.OrdinalIgnoreCase))
        {
            return StationRole.KioskRotation;
        }

        if (processName.Contains("CashBox", StringComparison.OrdinalIgnoreCase))
        {
            return StationRole.CashBox;
        }

        if (processName.Contains("Operations", StringComparison.OrdinalIgnoreCase))
        {
            return StationRole.OperationsHost;
        }

        return null;
    }

    private static StationRole? ParseRoleOrNull(string? value)
    {
        return Enum.TryParse<StationRole>(value, ignoreCase: true, out var role)
            ? role
            : null;
    }

    private static StationSettingsFile? LoadFromFile()
    {
        var path = Path.Combine(LocalAppPaths.ConfigDirectory, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StationSettingsFile>(
                File.ReadAllText(path),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() },
                });
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string NormalizeBaseUrl(string? value, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return candidate.EndsWith("/", StringComparison.Ordinal) ? candidate[..^1] : candidate;
    }

    private sealed record StationSettingsFile(
        StationRole Role,
        string? LanServerUrl,
        string? LanListenUrl,
        string? DeviceId,
        string? LanSharedSecret,
        bool StartLanHostInDevelopment);
}
