using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Barberia.Desktop.Services;
using Xunit;

namespace Barberia.Desktop.Tests;

public sealed class PayrollServiceTests
{
    [Fact]
    public void GetWeekRange_UsesFridayToThursdayPayrollWeek()
    {
        using var database = TestDatabase.Create();
        var service = new PayrollService(database.ConnectionFactory);

        var range = service.GetWeekRange(DateTimeOffset.Parse("2026-06-10T15:00:00-04:00"));

        Assert.Equal(DateTimeOffset.Parse("2026-06-05T00:00:00-04:00"), range.Start);
        Assert.Equal(DateTimeOffset.Parse("2026-06-12T00:00:00-04:00"), range.End);
    }

    [Fact]
    public void GeneratePreview_GroupsCommissionLinesByBarber()
    {
        using var database = TestDatabase.Create();
        var friday = DateTimeOffset.Parse("2026-06-05T00:00:00Z");
        var anaId = Guid.NewGuid();
        var luisId = Guid.NewGuid();
        SeedBarberTurnAndPayment(database, anaId, "Ana", 1, friday.AddHours(10), 2500, 1600);
        SeedBarberTurnAndPayment(database, anaId, "Ana", 1, friday.AddHours(11), 1500, 900);
        SeedBarberTurnAndPayment(database, luisId, "Luis", 2, friday.AddHours(12), 3000, 1950);
        SeedBarberTurnAndPayment(database, luisId, "Luis", 2, friday.AddHours(13), 3000, null);

        var snapshot = new PayrollService(database.ConnectionFactory)
            .GeneratePreview(new PayrollWeekRange(friday, friday.AddDays(7)), [], friday.AddDays(1));

        Assert.Equal(3, snapshot.Period.TotalServices);
        Assert.Equal(4450, snapshot.Period.TotalCommissionCents);
        Assert.Collection(
            snapshot.Lines,
            line =>
            {
                Assert.Equal(anaId, line.BarberId);
                Assert.Equal(2, line.ClosedServicesCount);
                Assert.Equal(4000, line.SalesGeneratedCents);
                Assert.Equal(2500, line.CommissionCents);
            },
            line =>
            {
                Assert.Equal(luisId, line.BarberId);
                Assert.Equal(1, line.ClosedServicesCount);
                Assert.Equal(3000, line.SalesGeneratedCents);
                Assert.Equal(1950, line.CommissionCents);
            });
    }

    [Fact]
    public void GeneratePreview_IncludesTempAdjustments()
    {
        using var database = TestDatabase.Create();
        var friday = DateTimeOffset.Parse("2026-06-05T00:00:00Z");
        var anaId = Guid.NewGuid();
        var range = new PayrollWeekRange(friday, friday.AddDays(7));
        SeedBarberTurnAndPayment(database, anaId, "Ana", 1, friday.AddHours(10), 2500, 1600);
        var service = new PayrollService(database.ConnectionFactory);

        var adjustments = new[] { new PayrollAdjustment(Guid.NewGuid(), Guid.Empty, anaId, -500, "Correccion", friday.AddDays(1).AddMinutes(1)) };
        var snapshot = service.GeneratePreview(range, adjustments, friday.AddDays(1).AddMinutes(2));

        var line = Assert.Single(snapshot.Lines);
        Assert.Equal(-500, line.AdjustmentsCents);
        Assert.Equal(1100, line.TotalCents);
        Assert.Equal(1100, snapshot.Period.TotalToPayCents);
    }

    [Fact]
    public void PayPeriod_MarksPeriodPaidAndAuditsEvent()
    {
        using var database = TestDatabase.Create();
        var friday = DateTimeOffset.Parse("2026-06-05T00:00:00Z");
        var anaId = Guid.NewGuid();
        var range = new PayrollWeekRange(friday, friday.AddDays(7));
        SeedBarberTurnAndPayment(database, anaId, "Ana", 1, friday.AddHours(10), 2500, 1600);
        var service = new PayrollService(database.ConnectionFactory);

        var snapshot = service.PayPeriod(range, [], PayrollPaymentMethod.Transfer, "TR-1", null, friday.AddDays(7));

        Assert.Equal(PayrollPeriodState.Paid, snapshot.Period.State);
        Assert.Equal(PayrollPaymentMethod.Transfer, snapshot.Period.PaymentMethod);
        using var connection = database.ConnectionFactory.OpenConnection();
        Assert.Contains(new AuditEventRepository(connection).ListAll(), audit => audit.EventType == "PayrollPeriodPaid");
        Assert.Empty(new PayrollRepository(connection).GetUnpaidPayments(friday, friday.AddDays(7)));
    }

    [Fact]
    public void PaidPeriod_CannotBeRecalculatedOrPaidAgain()
    {
        using var database = TestDatabase.Create();
        var friday = DateTimeOffset.Parse("2026-06-05T00:00:00Z");
        var anaId = Guid.NewGuid();
        var range = new PayrollWeekRange(friday, friday.AddDays(7));
        SeedBarberTurnAndPayment(database, anaId, "Ana", 1, friday.AddHours(10), 2500, 1600);
        var service = new PayrollService(database.ConnectionFactory);

        service.PayPeriod(range, [], PayrollPaymentMethod.Cash, null, null, friday.AddDays(7));

        Assert.Throws<InvalidOperationException>(() => service.GeneratePreview(range, [], friday.AddDays(7).AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => service.PayPeriod(range, [], PayrollPaymentMethod.Cash, null, null, friday.AddDays(7).AddMinutes(3)));
    }

    private static void SeedBarberTurnAndPayment(
        TestDatabase database,
        Guid barberId,
        string barberName,
        int stationNumber,
        DateTimeOffset collectedAt,
        long amountCents,
        long? commissionCents)
    {
        using var connection = database.ConnectionFactory.OpenConnection();
        var barberRepository = new LocalBarberRepository(connection);
        if (barberRepository.GetById(barberId) is null)
        {
            barberRepository.Upsert(new Barber(barberId, barberName, BarberState.Available, 0, 0, collectedAt, stationNumber: stationNumber), collectedAt);
        }

        var turnId = Guid.NewGuid();
        new LocalTurnRepository(connection).Upsert(
            new Turn(
                turnId,
                $"T-{Guid.NewGuid():N}",
                Random.Shared.Next(1, 1_000_000),
                DateOnly.FromDateTime(collectedAt.LocalDateTime),
                TurnState.Completed,
                TurnSource.WalkIn,
                collectedAt,
                barberId),
            collectedAt);
        new CashPaymentRepository(connection).Add(new CashPayment(
            Guid.NewGuid(),
            turnId,
            barberId,
            amountCents,
            "USD",
            collectedAt,
            "autocaja-1",
            null,
            true,
            commissionCents));
    }

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
