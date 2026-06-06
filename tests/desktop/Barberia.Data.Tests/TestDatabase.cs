using Microsoft.Data.Sqlite;

namespace Barberia.Data.Tests;

internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _keepAliveConnection;

    private TestDatabase(SqliteConnectionFactory connectionFactory, SqliteConnection keepAliveConnection)
    {
        ConnectionFactory = connectionFactory;
        _keepAliveConnection = keepAliveConnection;
        Connection = connectionFactory.OpenConnection();
    }

    public SqliteConnectionFactory ConnectionFactory { get; }

    public SqliteConnection Connection { get; }

    public static TestDatabase Create()
    {
        return Create(initialize: true);
    }

    public static TestDatabase CreateUninitialized()
    {
        return Create(initialize: false);
    }

    private static TestDatabase Create(bool initialize)
    {
        var name = Guid.NewGuid().ToString("N");
        var connectionString = $"Data Source={name};Mode=Memory;Cache=Shared";
        var connectionFactory = new SqliteConnectionFactory(connectionString);
        var keepAliveConnection = connectionFactory.OpenConnection();
        if (initialize)
        {
            LocalDatabaseInitializer.Initialize(keepAliveConnection);
        }

        return new TestDatabase(connectionFactory, keepAliveConnection);
    }

    public void Dispose()
    {
        Connection.Dispose();
        _keepAliveConnection.Dispose();
    }
}
