using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Barberia.Desktop.Services;
using Barberia.Hardware.Pos;
using Xunit;

namespace Barberia.Desktop.Tests;

public sealed class CashBoxPendingPaymentServiceTests
{
    [Fact]
    public void MarkServicePendingPayment_CreatesPendingRecordWithoutCashPaymentOrPayroll()
    {
        using var database = TestDatabase.Create();
        var scenario = SeedInServiceScenario(database);
        var service = CreateService(database);

        var cashBox = new CashBoxCloseService(
            database.ConnectionFactory,
            new SimulatedCashBoxReceiptPrinter(),
            new SimulatedCashDrawer());

        var result = cashBox.MarkServicePendingPayment(scenario.DisplayTicketNumber.ToString(), service.Id, 2m);

        using var connection = database.ConnectionFactory.OpenConnection();
        var pending = Assert.Single(new PendingServicePaymentRepository(connection).ListOpenByBusinessDate(scenario.BusinessDate));
        var payments = new CashPaymentRepository(connection).ListByTurn(scenario.TurnId);
        var unpaidPayroll = new PayrollRepository(connection).GetUnpaidPayments(scenario.Now.AddDays(-1), scenario.Now.AddDays(1));
        var turn = new LocalTurnRepository(connection).GetById(scenario.TurnId);
        var barber = new LocalBarberRepository(connection).GetById(scenario.BarberId);

        Assert.Equal(scenario.DisplayTicketNumber, result.DisplayTicketNumber);
        Assert.Equal(scenario.TurnId, pending.TurnId);
        Assert.Equal(2500, pending.AmountCents);
        Assert.Empty(payments);
        Assert.Empty(unpaidPayroll);
        Assert.Equal(TurnState.Completed, turn?.State);
        Assert.Equal(BarberState.Available, barber?.State);
        Assert.Equal(1, barber?.ClientsServedToday);
    }

    [Fact]
    public void CollectPendingPayments_CreatesCashPaymentIncludedInPayrollAndClosesPendingRecord()
    {
        using var database = TestDatabase.Create();
        var scenario = SeedInServiceScenario(database);
        var service = CreateService(database);
        var cashBox = new CashBoxCloseService(
            database.ConnectionFactory,
            new SimulatedCashBoxReceiptPrinter(),
            new SimulatedCashDrawer());
        cashBox.MarkServicePendingPayment(scenario.DisplayTicketNumber.ToString(), service.Id, 2m);

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            var pending = Assert.Single(new PendingServicePaymentRepository(connection).ListOpenByBusinessDate(scenario.BusinessDate));
            var collection = cashBox.CollectPendingPayments([pending.Id], CustomerPaymentMethod.Cash, null);

            Assert.Equal(1, collection.PaymentCount);
            Assert.Equal(25m, collection.TotalAmount);
        }

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var openPending = new PendingServicePaymentRepository(verifyConnection).ListOpenByBusinessDate(scenario.BusinessDate);
        var payments = new CashPaymentRepository(verifyConnection).ListByTurn(scenario.TurnId);
        var unpaidPayroll = new PayrollRepository(verifyConnection).GetUnpaidPayments(scenario.Now.AddDays(-1), scenario.Now.AddDays(1));

        var payment = Assert.Single(payments);
        Assert.Empty(openPending);
        Assert.Equal(2500, payment.AmountCents);
        Assert.Equal(1625, payment.CommissionCents);
        Assert.Single(unpaidPayroll);
    }

    [Fact]
    public void CollectPendingPayments_BlocksSecondCollection()
    {
        using var database = TestDatabase.Create();
        var scenario = SeedInServiceScenario(database);
        var service = CreateService(database);
        var cashBox = new CashBoxCloseService(
            database.ConnectionFactory,
            new SimulatedCashBoxReceiptPrinter(),
            new SimulatedCashDrawer());
        cashBox.MarkServicePendingPayment(scenario.DisplayTicketNumber.ToString(), service.Id, 0m);

        Guid pendingId;
        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            pendingId = Assert.Single(new PendingServicePaymentRepository(connection).ListOpenByBusinessDate(scenario.BusinessDate)).Id;
        }

        cashBox.CollectPendingPayments([pendingId], CustomerPaymentMethod.Cash, null);

        Assert.Throws<InvalidOperationException>(() =>
            cashBox.CollectPendingPayments([pendingId], CustomerPaymentMethod.Cash, null));
    }

    [Fact]
    public void MarkServicePendingPayment_AssignsNextWaitingTurn()
    {
        using var database = TestDatabase.Create();
        var scenario = SeedInServiceScenario(database);
        var service = CreateService(database);
        var nextBarberId = Guid.NewGuid();
        var waitingTurnId = Guid.NewGuid();

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            new LocalBarberRepository(connection).Upsert(
                new Barber(nextBarberId, "Ana", BarberState.Available, 0, 1, scenario.Now.AddHours(-1), stationNumber: 2),
                scenario.Now);
            new DailyRotationRepository(connection).EnsureQueued(scenario.BusinessDate, nextBarberId, scenario.Now.AddHours(-1), scenario.Now);
            new LocalTurnRepository(connection).Upsert(
                new Turn(
                    waitingTurnId,
                    $"W-{Guid.NewGuid():N}",
                    scenario.DisplayTicketNumber + 1,
                    scenario.BusinessDate,
                    TurnState.Waiting,
                    TurnSource.WalkIn,
                    scenario.Now.AddMinutes(-3),
                    customerName: "Next Customer"),
                scenario.Now);
        }

        var cashBox = new CashBoxCloseService(
            database.ConnectionFactory,
            new SimulatedCashBoxReceiptPrinter(),
            new SimulatedCashDrawer());

        cashBox.MarkServicePendingPayment(scenario.DisplayTicketNumber.ToString(), service.Id, 0m);

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var waitingTurn = new LocalTurnRepository(verifyConnection).GetById(waitingTurnId);
        var nextBarber = new LocalBarberRepository(verifyConnection).GetById(nextBarberId);

        Assert.Equal(TurnState.Called, waitingTurn?.State);
        Assert.Equal(nextBarberId, waitingTurn?.AssignedBarberId);
        Assert.Equal(BarberState.Called, nextBarber?.State);
    }

    private static InServiceScenario SeedInServiceScenario(TestDatabase database)
    {
        var now = GetNewJerseyNow();
        var businessDate = DateOnly.FromDateTime(now.DateTime);
        var barberId = Guid.NewGuid();
        var turnId = Guid.NewGuid();
        const int displayTicketNumber = 101;

        using var connection = database.ConnectionFactory.OpenConnection();
        new LocalBarberRepository(connection).Upsert(
            new Barber(barberId, "Luis", BarberState.InService, 0, 0, now.AddHours(-2), stationNumber: 1),
            now);
        new DailyRotationRepository(connection).EnsureQueued(businessDate, barberId, now.AddHours(-2), now);
        new LocalTurnRepository(connection).Upsert(
            new Turn(
                turnId,
                $"W-{Guid.NewGuid():N}",
                displayTicketNumber,
                businessDate,
                TurnState.InService,
                TurnSource.WalkIn,
                now.AddMinutes(-30),
                barberId,
                customerName: "Family Customer",
                startedAt: now.AddMinutes(-20)),
            now);

        return new InServiceScenario(now, businessDate, barberId, turnId, displayTicketNumber);
    }

    private static Service CreateService(TestDatabase database)
    {
        var now = GetNewJerseyNow();
        var service = new Service(Guid.NewGuid(), "Kids Cut", 23m, true, 1, now, now);
        using var connection = database.ConnectionFactory.OpenConnection();
        new ServiceRepository(connection).Add(service);
        return service;
    }

    private static DateTimeOffset GetNewJerseyNow()
    {
        try
        {
            return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
        }
    }

    private sealed record InServiceScenario(
        DateTimeOffset Now,
        DateOnly BusinessDate,
        Guid BarberId,
        Guid TurnId,
        int DisplayTicketNumber);

    private sealed class TestDatabase : IDisposable
    {
        private readonly IDisposable _keepAliveConnection;

        private TestDatabase(SqliteConnectionFactory connectionFactory, IDisposable keepAliveConnection)
        {
            ConnectionFactory = connectionFactory;
            _keepAliveConnection = keepAliveConnection;
        }

        public SqliteConnectionFactory ConnectionFactory { get; }

        public static TestDatabase Create()
        {
            var name = Guid.NewGuid().ToString("N");
            var connectionFactory = new SqliteConnectionFactory($"Data Source={name};Mode=Memory;Cache=Shared");
            var keepAliveConnection = connectionFactory.OpenConnection();
            LocalDatabaseInitializer.Initialize(keepAliveConnection);

            return new TestDatabase(connectionFactory, keepAliveConnection);
        }

        public void Dispose()
        {
            _keepAliveConnection.Dispose();
        }
    }
}
