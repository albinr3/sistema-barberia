using System.Text.Json;
using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Microsoft.Data.Sqlite;

namespace Barberia.Desktop.Services;

internal static class DailyOperationCoordinator
{
    public static DateOnly GetBusinessDate(DateTimeOffset now)
    {
        return OperationalClock.GetBusinessDate(now);
    }

    public static void EnsureDailyReset(SqliteConnectionFactory connectionFactory, DateTimeOffset now, string deviceId)
    {
        var transaction = new LocalDataTransaction(connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            EnsureDailyReset(connection, sqliteTransaction, now, deviceId);
        });
    }

    public static void EnsureDailyReset(
        SqliteConnection connection,
        SqliteTransaction sqliteTransaction,
        DateTimeOffset now,
        string deviceId)
    {
        var businessDate = GetBusinessDate(now);
        var stateRepository = new DailyOperationStateRepository(connection, sqliteTransaction);
        if (stateRepository.HasResetFor(businessDate))
        {
            return;
        }

        var turnRepository = new LocalTurnRepository(connection, sqliteTransaction);
        var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
        var rotationRepository = new DailyRotationRepository(connection, sqliteTransaction);
        var auditRepository = new AuditEventRepository(connection, sqliteTransaction);
        var activeBeforeToday = turnRepository.ListActiveBefore(businessDate);
        var hasStaleBarberState = barberRepository
            .ListAll()
            .Any(barber => barber.State is BarberState.Called or BarberState.InService
                ? barber.CheckedInAt is null || GetBusinessDate(barber.CheckedInAt.Value) < businessDate
                : barber.State == BarberState.Available
                    && barber.CheckedInAt is DateTimeOffset checkedInAt
                    && GetBusinessDate(checkedInAt) < businessDate);
        var lastResetDate = stateRepository.GetLastResetDate();

        if (lastResetDate is null && activeBeforeToday.Count == 0 && !hasStaleBarberState)
        {
            stateRepository.MarkResetApplied(businessDate, now);
            return;
        }

        foreach (var turn in activeBeforeToday)
        {
            turnRepository.MarkCancelled(turn.Id, now);
        }

        var resetBarbers = barberRepository.ResetDailyOperationalState(now);
        rotationRepository.DeleteForDate(businessDate);
        stateRepository.MarkResetApplied(businessDate, now);

        auditRepository.Add(new AuditEvent(
            Guid.NewGuid(),
            now,
            "daily_operational_reset",
            "operation_day",
            Guid.NewGuid(),
            JsonSerializer.Serialize(new
            {
                businessDate = businessDate.ToString("yyyy-MM-dd"),
                cancelledTickets = activeBeforeToday.Count,
                resetBarbers
            }),
            deviceId));
    }
}

internal static class DailyRotationQueue
{
    public static IReadOnlyList<Guid> Build(
        IEnumerable<Barber> barbers,
        IEnumerable<DailyRotationEntry> entries,
        DateOnly businessDate)
    {
        return OrderBarbers(barbers, entries, businessDate)
            .Select(barber => barber.Id)
            .ToArray();
    }

    public static IReadOnlyList<Barber> CheckedInBarbers(
        IEnumerable<Barber> barbers,
        IEnumerable<DailyRotationEntry> entries,
        DateOnly businessDate)
    {
        var checkedInBarberIds = entries
            .Where(entry => entry.BusinessDate == businessDate)
            .Select(entry => entry.BarberId)
            .ToHashSet();

        return OrderBarbers(
            barbers.Where(barber => checkedInBarberIds.Contains(barber.Id)),
            entries,
            businessDate);
    }

    public static bool HasCheckedInToday(
        Barber barber,
        IEnumerable<DailyRotationEntry> entries,
        DateOnly businessDate)
    {
        return entries.Any(entry => entry.BusinessDate == businessDate && entry.BarberId == barber.Id);
    }

    public static IReadOnlyList<Barber> OrderBarbers(
        IEnumerable<Barber> barbers,
        IEnumerable<DailyRotationEntry> entries,
        DateOnly businessDate)
    {
        var queuePositions = entries.ToDictionary(entry => entry.BarberId, entry => entry.QueuePosition);

        return barbers
            .OrderBy(barber => QueueIndexOrMax(barber.Id, queuePositions))
            .ThenBy(barber => SameDayArrivalOrMax(barber, businessDate))
            .ThenBy(barber => barber.StationNumber ?? int.MaxValue)
            .ThenBy(barber => barber.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(barber => barber.Id)
            .ToArray();
    }

    private static int QueueIndexOrMax(Guid barberId, IReadOnlyDictionary<Guid, int> queuePositions)
    {
        return queuePositions.TryGetValue(barberId, out var position) ? position : int.MaxValue;
    }

    private static DateTimeOffset SameDayArrivalOrMax(Barber barber, DateOnly businessDate)
    {
        return barber.CheckedInAt is DateTimeOffset checkedInAt
            && OperationalClock.GetBusinessDate(checkedInAt) == businessDate
                ? checkedInAt
                : DateTimeOffset.MaxValue;
    }
}
