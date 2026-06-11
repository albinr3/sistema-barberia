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

    public PayrollSnapshot LoadOrGenerate(DateTimeOffset reference, IReadOnlyList<PayrollAdjustment> tempAdjustments)
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
                repository.ListAdjustments(existingPeriod.Id),
                DateTimeOffset.Now);
        }

        return GeneratePreview(range, tempAdjustments, DateTimeOffset.Now);
    }

    public PayrollSnapshot GeneratePreview(PayrollWeekRange range, IReadOnlyList<PayrollAdjustment> tempAdjustments, DateTimeOffset generatedAt)
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
        
        var periodAdjustments = tempAdjustments.Select(adj => adj with { PeriodId = periodId }).ToList();
        var lines = BuildLines(periodId, payments, periodAdjustments, barbers);

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

        snapshot = new PayrollSnapshot(period, lines, periodAdjustments, generatedAt);

        return snapshot ?? throw new InvalidOperationException("Payroll snapshot could not be generated.");
    }

    public PayrollSnapshot PayPeriod(
        PayrollWeekRange range,
        IReadOnlyList<PayrollAdjustment> tempAdjustments,
        PayrollPaymentMethod method,
        string? reference,
        string? notes,
        DateTimeOffset paidAt)
    {
        var snapshot = GeneratePreview(range, tempAdjustments, paidAt);
        if (snapshot.Period.State == PayrollPeriodState.Paid)
        {
            throw new InvalidOperationException("Payroll period is already paid.");
        }

        new LocalDataTransaction(_connectionFactory).Execute((connection, transaction) =>
        {
            var repository = new PayrollRepository(connection, transaction);
            
            repository.SavePeriod(snapshot.Period, snapshot.Lines);
            foreach (var adj in snapshot.Adjustments)
            {
                repository.AddAdjustment(adj);
            }

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
            repository.ListAdjustments(period.Id),
            DateTimeOffset.Now);
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

    public IReadOnlyList<PayrollDailyBreakdown> GetBarberDailyBreakdown(Guid periodId, Guid barberId)
    {
        using var connection = _connectionFactory.OpenConnection();
        var repository = new PayrollRepository(connection);
        var period = repository.GetPeriod(periodId) 
            ?? throw new InvalidOperationException("Payroll period was not found.");

        var payments = repository.GetPaymentsForPeriod(period, barberId);

        var grouped = payments.GroupBy(p => p.CollectedAt.Date)
            .Select(g => new PayrollDailyBreakdown(
                new DateTimeOffset(g.Key, period.StartDate.Offset),
                g.Count(),
                g.Sum(p => p.AmountCents),
                g.Sum(p => p.CommissionCents ?? 0)
            ))
            .OrderBy(b => b.Date)
            .ToList();

        return grouped;
    }

    private static IReadOnlyList<PayrollLine> BuildLines(
        Guid periodId,
        IReadOnlyList<CashPayment> payments,
        IReadOnlyList<PayrollAdjustment> adjustments,
        IReadOnlyList<Core.Domain.Barber> barbers)
    {
        var barberIds = payments.Select(payment => payment.BarberId)
            .Concat(adjustments.Select(adjustment => adjustment.BarberId))
            .Distinct()
            .ToList();

        return barberIds
            .Select(barberId =>
            {
                var barber = barbers.FirstOrDefault(candidate => candidate.Id == barberId);
                var barberPayments = payments.Where(payment => payment.BarberId == barberId).ToList();
                var adjustmentTotal = adjustments
                    .Where(adjustment => adjustment.BarberId == barberId)
                    .Sum(adjustment => adjustment.AmountCents);
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
                    adjustmentTotal,
                    commissionTotal + adjustmentTotal);
            })
            .OrderBy(line => line.BarberName, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
    IReadOnlyList<PayrollAdjustment> Adjustments,
    DateTimeOffset LoadedAt);

public sealed record PayrollBarberOption(Guid Id, string DisplayName);

public sealed record PayrollDailyBreakdown(
    DateTimeOffset Date,
    int ServicesCount,
    long SalesCents,
    long CommissionCents);
