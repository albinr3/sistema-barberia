namespace Barberia.Desktop.Services;

internal static class LocalAppPaths
{
    private const string ApplicationDirectoryName = "BarberiaSystem";
    private const string DatabaseFileName = "barberia-local.db";
    private const string ErrorLogFileName = "Barberia.Desktop.error.log";

    public static string RootDirectory => EnsureDirectory(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ApplicationDirectoryName));

    public static string ConfigDirectory => EnsureDirectory(Path.Combine(RootDirectory, "config"));

    public static string LogsDirectory => EnsureDirectory(Path.Combine(RootDirectory, "logs"));

    public static string ProfileImagesDirectory => EnsureDirectory(Path.Combine(RootDirectory, "profile-images"));

    public static string DatabasePath => Path.Combine(RootDirectory, DatabaseFileName);

    public static string ErrorLogPath => Path.Combine(LogsDirectory, ErrorLogFileName);

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
