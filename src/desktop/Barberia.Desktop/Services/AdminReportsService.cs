using Barberia.Data;
using Barberia.Data.Reports;

namespace Barberia.Desktop.Services;

public sealed class AdminReportsService
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public AdminReportsService()
        : this(LocalDesktopDatabase.CreateConnectionFactory())
    {
    }

    public AdminReportsService(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        new LocalDatabaseInitializer(_connectionFactory).Initialize();
    }

    public LocalAdminReportSnapshot LoadDailyReport(DateTimeOffset selectedDate)
    {
        var from = new DateTimeOffset(
            selectedDate.Year,
            selectedDate.Month,
            selectedDate.Day,
            0,
            0,
            0,
            selectedDate.Offset);
        var to = from.AddDays(1);

        using var connection = _connectionFactory.OpenConnection();
        return new LocalAdminReportRepository(connection).Load(from, to, DateTimeOffset.Now);
    }
}
