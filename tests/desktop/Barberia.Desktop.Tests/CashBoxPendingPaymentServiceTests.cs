using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Barberia.Desktop.Services;
using Barberia.Hardware.Pos;
using System.Text.Json;
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
    public void MarkServicePendingPayment_DoesNotPrintReceiptOrOpenDrawer()
    {
        using var database = TestDatabase.Create();
        var scenario = SeedInServiceScenario(database);
        var service = CreateService(database);
        var printer = new RecordingCashBoxReceiptPrinter();
        var drawer = new RecordingCashDrawer();
        var cashBox = new CashBoxCloseService(database.ConnectionFactory, printer, drawer);

        cashBox.MarkServicePendingPayment(scenario.DisplayTicketNumber.ToString(), service.Id, 0m);

        Assert.Equal(0, printer.PrintCount);
        Assert.Equal(0, drawer.OpenCount);
    }

    [Fact]
    public void MarkServicePendingPayment_UsesStoredServiceIdFormatForForeignKeys()
    {
        using var database = TestDatabase.Create();
        var scenario = SeedInServiceScenario(database);
        var service = CreateServiceWithCompactStoredId(database);

        var cashBox = new CashBoxCloseService(
            database.ConnectionFactory,
            new SimulatedCashBoxReceiptPrinter(),
            new SimulatedCashDrawer());

        cashBox.MarkServicePendingPayment(scenario.DisplayTicketNumber.ToString(), service.Id, 0m);

        using var connection = database.ConnectionFactory.OpenConnection();
        var pending = Assert.Single(new PendingServicePaymentRepository(connection).ListOpenByBusinessDate(scenario.BusinessDate));
        Assert.Equal(service.Id, pending.ServiceId);
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
            var collection = cashBox.CollectPendingPayments([pending.Id], CustomerPaymentMethod.Cash, null, 1);

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
    public void CollectPendingPayments_RequiresActiveCollectorStationWithoutClosingPendingPayment()
    {
        using var database = TestDatabase.Create();
        var scenario = SeedInServiceScenario(database);
        var service = CreateService(database);
        var printer = new RecordingCashBoxReceiptPrinter();
        var cashBox = new CashBoxCloseService(database.ConnectionFactory, printer, new RecordingCashDrawer());
        cashBox.MarkServicePendingPayment(scenario.DisplayTicketNumber.ToString(), service.Id, 0m);

        Guid pendingId;
        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            pendingId = Assert.Single(new PendingServicePaymentRepository(connection).ListOpenByBusinessDate(scenario.BusinessDate)).Id;
        }

        var exception = Assert.Throws<InvalidOperationException>(() =>
            cashBox.CollectPendingPayments([pendingId], CustomerPaymentMethod.Cash, null, 99));

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        Assert.Contains("No active barber is assigned to B-99", exception.Message);
        Assert.Single(new PendingServicePaymentRepository(verifyConnection).ListOpenByBusinessDate(scenario.BusinessDate));
        Assert.Empty(new CashPaymentRepository(verifyConnection).ListByTurn(scenario.TurnId));
        Assert.Equal(0, printer.PrintCount);
    }

    [Fact]
    public void CollectPendingPayments_RecordsCollectorButPaysOriginalBarbersAndPrintsTicketLines()
    {
        using var database = TestDatabase.Create();
        var firstScenario = SeedInServiceScenario(database, stationNumber: 1, barberName: "Luis", displayTicketNumber: 101);
        var secondScenario = SeedInServiceScenario(database, stationNumber: 2, barberName: "Ana", displayTicketNumber: 102);
        var service = CreateService(database);
        var printer = new RecordingCashBoxReceiptPrinter();
        var cashBox = new CashBoxCloseService(database.ConnectionFactory, printer, new RecordingCashDrawer());

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            new LocalBarberRepository(connection).Upsert(
                new Barber(Guid.NewGuid(), "Franklin", BarberState.Available, 0, 9, firstScenario.Now.AddHours(-1), stationNumber: 9),
                firstScenario.Now);
        }

        cashBox.MarkServicePendingPayment(firstScenario.DisplayTicketNumber.ToString(), service.Id, 0m);
        cashBox.MarkServicePendingPayment(secondScenario.DisplayTicketNumber.ToString(), service.Id, 2m);

        Guid[] pendingIds;
        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            pendingIds = new PendingServicePaymentRepository(connection)
                .ListOpenByBusinessDate(firstScenario.BusinessDate)
                .Select(row => row.Id)
                .ToArray();
        }

        var result = cashBox.CollectPendingPayments(pendingIds, CustomerPaymentMethod.Cash, null, 9);

        using var verifyConnection = database.ConnectionFactory.OpenConnection();
        var firstPayment = Assert.Single(new CashPaymentRepository(verifyConnection).ListByTurn(firstScenario.TurnId));
        var secondPayment = Assert.Single(new CashPaymentRepository(verifyConnection).ListByTurn(secondScenario.TurnId));
        var audit = new AuditEventRepository(verifyConnection)
            .ListAll()
            .Single(auditEvent => auditEvent.EventType == "cash_box_pending_payments_collected");
        using var payload = JsonDocument.Parse(audit.Payload);

        Assert.Equal("B-9", result.CollectorBarberStationCode);
        Assert.Equal(firstScenario.BarberId, firstPayment.BarberId);
        Assert.Equal(secondScenario.BarberId, secondPayment.BarberId);
        Assert.Equal(1, printer.PrintCount);
        Assert.NotNull(printer.LastJob);
        Assert.Equal(2, printer.LastJob!.Lines?.Count);
        Assert.Equal(result.TotalAmount, printer.LastJob.Lines!.Sum(line => line.Amount));
        Assert.Equal("Franklin", printer.LastJob.CollectedByName);
        Assert.Equal("B-9", printer.LastJob.CollectedByStationCode);
        Assert.Equal("Franklin", payload.RootElement.GetProperty("collectorBarberName").GetString());
        Assert.Equal("B-9", payload.RootElement.GetProperty("collectorStationCode").GetString());
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

        cashBox.CollectPendingPayments([pendingId], CustomerPaymentMethod.Cash, null, 1);

        Assert.Throws<InvalidOperationException>(() =>
            cashBox.CollectPendingPayments([pendingId], CustomerPaymentMethod.Cash, null, 1));
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

    private static InServiceScenario SeedInServiceScenario(TestDatabase database, int stationNumber = 1, string barberName = "Luis", int displayTicketNumber = 101)
    {
        var now = GetNewJerseyNow();
        var businessDate = DateOnly.FromDateTime(now.DateTime);
        var barberId = Guid.NewGuid();
        var turnId = Guid.NewGuid();

        using var connection = database.ConnectionFactory.OpenConnection();
        new LocalBarberRepository(connection).Upsert(
            new Barber(barberId, barberName, BarberState.InService, 0, stationNumber - 1, now.AddHours(-2), stationNumber: stationNumber),
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

    private static Service CreateServiceWithCompactStoredId(TestDatabase database)
    {
        var now = GetNewJerseyNow();
        var service = new Service(Guid.NewGuid(), "Compact Id Cut", 25m, true, 1, now, now);
        using var connection = database.ConnectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO services (id, name, price_cents, is_active, display_order, created_at, updated_at)
            VALUES ($id, $name, $price_cents, $is_active, $display_order, $created_at, $updated_at);
            """;
        command.Parameters.AddWithValue("$id", service.Id.ToString("N"));
        command.Parameters.AddWithValue("$name", service.Name);
        command.Parameters.AddWithValue("$price_cents", service.PriceCents);
        command.Parameters.AddWithValue("$is_active", 1);
        command.Parameters.AddWithValue("$display_order", service.DisplayOrder);
        command.Parameters.AddWithValue("$created_at", service.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated_at", service.UpdatedAt.ToString("O"));
        command.ExecuteNonQuery();
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

    private sealed class RecordingCashBoxReceiptPrinter : ICashBoxReceiptPrinter
    {
        public int PrintCount { get; private set; }

        public CashReceiptPrintJob? LastJob { get; private set; }

        public HardwareOperationResult Print(CashReceiptPrintJob job)
        {
            PrintCount++;
            LastJob = job;
            return HardwareOperationResult.Success();
        }

        public HardwareOperationResult PrintDayReport(DayReportPrintJob job)
        {
            return HardwareOperationResult.Success();
        }
    }

    private sealed class RecordingCashDrawer : ICashDrawer
    {
        public int OpenCount { get; private set; }

        public HardwareOperationResult Open(string deviceId)
        {
            OpenCount++;
            return HardwareOperationResult.Success();
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
