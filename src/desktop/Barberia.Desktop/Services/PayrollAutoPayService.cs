using Barberia.Data;

namespace Barberia.Desktop.Services;

internal sealed class PayrollAutoPayService : IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _loopTask;

    public PayrollAutoPayService()
        : this(LocalDesktopDatabase.CreateConnectionFactory())
    {
    }

    public PayrollAutoPayService(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        new LocalDatabaseInitializer(_connectionFactory).Initialize();
    }

    public void Start()
    {
        _loopTask = Task.Run(() => RunLoopAsync(_cancellationTokenSource.Token));
    }

    internal PayrollSnapshot? RunOnce(DateTimeOffset now)
    {
        try
        {
            var snapshot = new PayrollService(_connectionFactory).AutoPayLastClosedPeriod(now);
            if (snapshot is not null)
            {
                Log($"Payroll auto-paid for {snapshot.Period.StartDate:yyyy-MM-dd} to {snapshot.Period.EndDate:yyyy-MM-dd}.");
            }

            return snapshot;
        }
        catch (Exception exception)
        {
            Log($"Payroll autopay skipped: {exception.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        RunOnce(OperationalClock.Now);

        using var timer = new PeriodicTimer(CheckInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            RunOnce(OperationalClock.Now);
        }
    }

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(
                LocalAppPaths.ErrorLogPath,
                $"[{OperationalClock.Now:O}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
