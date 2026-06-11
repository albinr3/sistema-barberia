using Barberia.Core.Domain;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Barberia.Data.Tests;

public sealed class PayrollRepositoryTests
{
    [Fact]
    public void GetUnpaidPayments_FiltersDateRangeAndMissingCommission()
    {
        using var database = TestDatabase.Create();
        var friday = DateTimeOffset.Parse("2026-06-05T00:00:00Z");
        var barberId = Guid.NewGuid();
        var includedPaymentId = SeedBarberTurnAndPayment(database, barberId, friday.AddHours(10), 2500, 1600);

        SeedBarberTurnAndPayment(database, barberId, friday.AddDays(-1), 2500, 1600);
        SeedBarberTurnAndPayment(database, barberId, friday.AddHours(12), 2500, null);
        SeedBarberTurnAndPayment(database, barberId, friday.AddDays(7), 2500, 1600);

        var payments = new PayrollRepository(database.Connection)
            .GetUnpaidPayments(friday, friday.AddDays(7));

        var payment = Assert.Single(payments);
        Assert.Equal(includedPaymentId, payment.Id);
        Assert.Equal(1600, payment.CommissionCents);
    }

    [Fact]
    public void MarkAsPaid_RegistersPaymentItemsAndExcludesPaidPayments()
    {
        using var database = TestDatabase.Create();
        var friday = DateTimeOffset.Parse("2026-06-05T00:00:00Z");
        var barberId = Guid.NewGuid();
        var paymentId = SeedBarberTurnAndPayment(database, barberId, friday.AddHours(10), 2500, 1600);
        var repository = new PayrollRepository(database.Connection);
        var period = CreatePeriod(friday, friday.AddDays(7), totalServices: 1, totalCommission: 1600);

        repository.SavePeriod(period, [
            new PayrollLine(Guid.NewGuid(), period.Id, barberId, "Ana", null, 1, 1, 2500, 1600, 0, 1600)
        ]);
        repository.MarkAsPaid(period.Id, PayrollPaymentMethod.Cash, "REF-1", null, friday.AddDays(7), "test");

        Assert.Empty(repository.GetUnpaidPayments(friday, friday.AddDays(7)));
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT payment_id FROM payroll_payment_items WHERE period_id = $period_id";
        command.Parameters.AddWithValue("$period_id", period.Id.ToString());

        Assert.Equal(paymentId.ToString(), command.ExecuteScalar());
    }

    [Fact]
    public void SavePeriod_RejectsPaidPeriodRecalculation()
    {
        using var database = TestDatabase.Create();
        var friday = DateTimeOffset.Parse("2026-06-05T00:00:00Z");
        var barberId = Guid.NewGuid();
        SeedBarberTurnAndPayment(database, barberId, friday.AddHours(10), 2500, 1600);
        var repository = new PayrollRepository(database.Connection);
        var period = CreatePeriod(friday, friday.AddDays(7), totalServices: 1, totalCommission: 1600);

        repository.SavePeriod(period, [
            new PayrollLine(Guid.NewGuid(), period.Id, barberId, "Ana", null, 1, 1, 2500, 1600, 0, 1600)
        ]);
        repository.MarkAsPaid(period.Id, PayrollPaymentMethod.Cash, null, null, friday.AddDays(7), "test");

        Assert.Throws<InvalidOperationException>(() =>
            repository.SavePeriod(period with { TotalToPayCents = 2000 }, []));
    }

    [Fact]
    public void PayrollIndexes_PreventDuplicatePeriodAndPaymentItem()
    {
        using var database = TestDatabase.Create();
        var friday = DateTimeOffset.Parse("2026-06-05T00:00:00Z");
        var barberId = Guid.NewGuid();
        var paymentId = SeedBarberTurnAndPayment(database, barberId, friday.AddHours(10), 2500, 1600);
        var repository = new PayrollRepository(database.Connection);
        var period = CreatePeriod(friday, friday.AddDays(7), totalServices: 1, totalCommission: 1600);

        repository.SavePeriod(period, []);
        Assert.Throws<SqliteException>(() =>
            repository.SavePeriod(CreatePeriod(friday, friday.AddDays(7), totalServices: 0, totalCommission: 0), []));

        InsertPaymentItem(database, period.Id, barberId, paymentId);
        Assert.Throws<SqliteException>(() =>
            InsertPaymentItem(database, Guid.NewGuid(), barberId, paymentId));
    }

    private static PayrollPeriod CreatePeriod(
        DateTimeOffset start,
        DateTimeOffset end,
        int totalServices,
        long totalCommission)
    {
        return new PayrollPeriod(
            Guid.NewGuid(),
            start,
            end,
            PayrollPeriodState.Draft,
            totalServices,
            totalCommission,
            0,
            totalCommission,
            null,
            null,
            null,
            start,
            null);
    }

    private static Guid SeedBarberTurnAndPayment(
        TestDatabase database,
        Guid barberId,
        DateTimeOffset collectedAt,
        long amountCents,
        long? commissionCents)
    {
        var turnId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var barberRepository = new LocalBarberRepository(database.Connection);
        if (barberRepository.GetById(barberId) is null)
        {
            barberRepository.Upsert(new Barber(barberId, "Ana", BarberState.Available, 0, 0, collectedAt, stationNumber: 1), collectedAt);
        }

        new LocalTurnRepository(database.Connection).Upsert(
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
        new CashPaymentRepository(database.Connection).Add(new CashPayment(
            paymentId,
            turnId,
            barberId,
            amountCents,
            "USD",
            collectedAt,
            "autocaja-1",
            null,
            true,
            commissionCents));

        return paymentId;
    }

    private static void InsertPaymentItem(TestDatabase database, Guid periodId, Guid barberId, Guid paymentId)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO payroll_payment_items (id, period_id, barber_id, payment_id)
            VALUES ($id, $period_id, $barber_id, $payment_id);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$period_id", periodId.ToString());
        command.Parameters.AddWithValue("$barber_id", barberId.ToString());
        command.Parameters.AddWithValue("$payment_id", paymentId.ToString());
        command.ExecuteNonQuery();
    }
}
