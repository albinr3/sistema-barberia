using Microsoft.Data.Sqlite;

namespace Barberia.Data;

public sealed class LocalDataTransaction
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public LocalDataTransaction(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Execute(Action<SqliteConnection, SqliteTransaction> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        using var connection = _connectionFactory.OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            work(connection, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
