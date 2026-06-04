using Barberia.Data;

namespace Barberia.Desktop.Services;

internal static class LocalDesktopDatabase
{
    public static SqliteConnectionFactory CreateConnectionFactory()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BarberiaSystem");
        Directory.CreateDirectory(dataDirectory);

        var databasePath = Path.Combine(dataDirectory, "barberia-local.db");
        return new SqliteConnectionFactory($"Data Source={databasePath}");
    }
}
