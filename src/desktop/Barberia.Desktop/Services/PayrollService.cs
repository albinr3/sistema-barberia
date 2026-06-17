using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;

namespace Barberia.Desktop.Services;

public sealed class PayrollService
{
    private const string DeviceId = "local-payroll";
    private readonly SqliteConnectionFactory _connectionFactory;

    public PayrollService(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public PayrollWeekRange GetWeekRange(DateTimeOffset reference)
    {
        var localDate = reference.Date;
        var daysSinceFriday = ((int)localDate.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;
        var start = new DateTimeOffset(localDate.AddDays(-daysSinceFriday), reference.Offset);
        return new PayrollWeekRange(start, start.AddDays(7));
    }

    public PayrollSnapshot LoadOrGenerate(DateTimeOffset reference)
    {
        var range = GetWeekRange(reference);

        using var connection = _connectionFactory.OpenConnection();
        var repository = new PayrollRepository(connection);
        var existingPeriod = repository.GetPeriodByDates(range.Start, range.End);

        if (existingPeriod?.State == PayrollPeriodState.Paid)
        {
            return new PayrollSnapshot(
                existingPeriod,
                repository.ListLines(existingPeriod.Id),
                OperationalClock.Now);
        }

        return GeneratePreview(range, OperationalClock.Now);
    }

    public PayrollSnapshot GeneratePreview(PayrollWeekRange range, DateTimeOffset generatedAt)
    {
        PayrollSnapshot? snapshot = null;
        using var connection = _connectionFactory.OpenConnection();
        var repository = new PayrollRepository(connection);
        
        var existingPeriod = repository.GetPeriodByDates(range.Start, range.End);
        if (existingPeriod?.State == PayrollPeriodState.Paid)
        {
            throw new InvalidOperationException("A paid payroll period cannot be recalculated.");
        }

        var periodId = existingPeriod?.Id ?? Guid.NewGuid();
        var payments = repository.GetUnpaidPayments(range.Start, range.End);
        var barbers = new LocalBarberRepository(connection).ListAll();
        
        var lines = BuildLines(periodId, payments, barbers);

        var period = new PayrollPeriod(
            periodId,
            range.Start,
            range.End,
            PayrollPeriodState.Draft,
            lines.Sum(line => line.ClosedServicesCount),
            lines.Sum(line => line.CommissionCents),
            lines.Sum(line => line.AdjustmentsCents),
            lines.Sum(line => line.TotalCents),
            null,
            null,
            null,
            generatedAt,
            null);

        snapshot = new PayrollSnapshot(period, lines, generatedAt);

        return snapshot ?? throw new InvalidOperationException("Payroll snapshot could not be generated.");
    }

    public PayrollSnapshot PayPeriod(
        PayrollWeekRange range,
        PayrollPaymentMethod method,
        string? reference,
        string? notes,
        DateTimeOffset paidAt)
    {
        var snapshot = GeneratePreview(range, paidAt);
        if (snapshot.Period.State == PayrollPeriodState.Paid)
        {
            throw new InvalidOperationException("Payroll period is already paid.");
        }

        new LocalDataTransaction(_connectionFactory).Execute((connection, transaction) =>
        {
            var repository = new PayrollRepository(connection, transaction);
            
            repository.SavePeriod(snapshot.Period, snapshot.Lines);

            repository.MarkAsPaid(
                snapshot.Period.Id,
                method,
                NormalizeOptionalText(reference),
                NormalizeOptionalText(notes),
                paidAt,
                DeviceId);
        });

        return Load(range);
    }

    public PayrollSnapshot Load(PayrollWeekRange range)
    {
        using var connection = _connectionFactory.OpenConnection();
        var repository = new PayrollRepository(connection);
        var period = repository.GetPeriodByDates(range.Start, range.End)
            ?? throw new InvalidOperationException("Payroll period was not found.");

        return new PayrollSnapshot(
            period,
            repository.ListLines(period.Id),
            OperationalClock.Now);
    }

    public PayrollSnapshot? AutoPayLastClosedPeriod(DateTimeOffset now)
    {
        var currentRange = GetWeekRange(now);
        var closedRange = new PayrollWeekRange(currentRange.Start.AddDays(-7), currentRange.Start);
        if (closedRange.End > now)
        {
            return null;
        }

        using (var connection = _connectionFactory.OpenConnection())
        {
            var existingPeriod = new PayrollRepository(connection).GetPeriodByDates(closedRange.Start, closedRange.End);
            if (existingPeriod?.State == PayrollPeriodState.Paid)
            {
                return null;
            }
        }

        var preview = GeneratePreview(closedRange, now);
        return PayPeriod(
            closedRange,
            PayrollPaymentMethod.Cash,
            BuildAutomaticReference(preview.Period),
            "Auto-paid by desktop weekly cutoff.",
            now);
    }

    public IReadOnlyList<PayrollBarberOption> ListBarbers()
    {
        using var connection = _connectionFactory.OpenConnection();
        return new LocalBarberRepository(connection)
            .ListAll()
            .OrderBy(barber => barber.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(barber => new PayrollBarberOption(
                barber.Id,
                barber.StationNumber is null
                    ? barber.DisplayName
                    : $"{barber.DisplayName} ({barber.StationCode})"))
            .ToList();
    }

    public IReadOnlyList<PayrollPeriod> ListHistoricalPeriods()
    {
        using var connection = _connectionFactory.OpenConnection();
        return new PayrollRepository(connection).ListPeriods();
    }

    public IReadOnlyList<PayrollDailyBreakdown> GetBarberDailyBreakdown(PayrollSnapshot snapshot, Guid barberId)
    {
        using var connection = _connectionFactory.OpenConnection();
        var repository = new PayrollRepository(connection);
        var payments = repository.GetPaymentsForPeriod(snapshot.Period, barberId);

        var dates = payments
            .Select(payment => payment.CollectedAt.Date)
            .Distinct()
            .OrderBy(date => date)
            .ToList();

        var grouped = dates
            .Select(date =>
            {
                var dayPayments = payments.Where(payment => payment.CollectedAt.Date == date).ToList();
                var commissionTotal = dayPayments.Sum(payment => payment.CommissionCents ?? 0);

                return new PayrollDailyBreakdown(
                    new DateTimeOffset(date, snapshot.Period.StartDate.Offset),
                    dayPayments.Count,
                    dayPayments.Sum(payment => payment.AmountCents),
                    commissionTotal,
                    0,
                    commissionTotal);
            })
            .ToList();

        return grouped;
    }

    private static IReadOnlyList<PayrollLine> BuildLines(
        Guid periodId,
        IReadOnlyList<CashPayment> payments,
        IReadOnlyList<Core.Domain.Barber> barbers)
    {
        var barberIds = payments.Select(payment => payment.BarberId)
            .Distinct()
            .ToList();

        return barberIds
            .Select(barberId =>
            {
                var barber = barbers.FirstOrDefault(candidate => candidate.Id == barberId);
                var barberPayments = payments.Where(payment => payment.BarberId == barberId).ToList();
                var commissionTotal = barberPayments.Sum(payment => payment.CommissionCents ?? 0);

                return new PayrollLine(
                    Guid.NewGuid(),
                    periodId,
                    barberId,
                    barber?.DisplayName ?? "Local barber",
                    barber?.ProfileImagePath,
                    barber?.StationNumber,
                    barberPayments.Count,
                    barberPayments.Sum(payment => payment.AmountCents),
                    commissionTotal,
                    0,
                    commissionTotal);
            })
            .OrderBy(line => line.BarberName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildAutomaticReference(PayrollPeriod period)
    {
        return $"NOM-{period.StartDate:yyMMdd}-{period.Id.ToString()[..4].ToUpperInvariant()}";
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

}

public sealed record PayrollWeekRange(DateTimeOffset Start, DateTimeOffset End);

public sealed record PayrollSnapshot(
    PayrollPeriod Period,
    IReadOnlyList<PayrollLine> Lines,
    DateTimeOffset LoadedAt);

public sealed record PayrollBarberOption(Guid Id, string DisplayName);

public sealed record PayrollDailyBreakdown(
    DateTimeOffset Date,
    int ServicesCount,
    long SalesCents,
    long CommissionCents,
    long AdjustmentsCents,
    long TotalEarningsCents);
