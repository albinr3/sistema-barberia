using Barberia.Data;

namespace Barberia.Desktop.Services;

internal static class LocalDesktopDatabase
{
    public static SqliteConnectionFactory CreateConnectionFactory()
    {
        return new SqliteConnectionFactory($"Data Source={LocalAppPaths.DatabasePath}");
    }
}
