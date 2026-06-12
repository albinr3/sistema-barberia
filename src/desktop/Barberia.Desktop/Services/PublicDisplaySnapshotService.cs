using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Repositories;

namespace Barberia.Desktop.Services;

public sealed class PublicDisplaySnapshotService
{
    private static readonly TimeSpan AppointmentLookAhead = TimeSpan.FromHours(4);

    private readonly SqliteConnectionFactory _connectionFactory;

    public PublicDisplaySnapshotService()
        : this(LocalDesktopDatabase.CreateConnectionFactory())
    {
    }

    public PublicDisplaySnapshotService(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        new LocalDatabaseInitializer(_connectionFactory).Initialize();
    }

    public PublicDisplaySnapshot Load()
    {
        var now = OperationalClock.Now;
        DailyOperationCoordinator.EnsureDailyReset(_connectionFactory, now, Environment.MachineName);
        var businessDate = DailyOperationCoordinator.GetBusinessDate(now);

        using var connection = _connectionFactory.OpenConnection();
        var turnRepository = new LocalTurnRepository(connection);
        var barberRepository = new LocalBarberRepository(connection);
        var appointmentRepository = new AppointmentReservationRepository(connection);
        var dailyRotationEntries = new DailyRotationRepository(connection).ListByDate(businessDate);

        var turns = turnRepository.ListActiveForPublicDisplay();
        var barbers = DailyRotationQueue.OrderBarbers(
            barberRepository
            .ListAll()
            .Where(barber => barber.IsActive && barber.State != BarberState.Offline),
            dailyRotationEntries,
            businessDate);
        var appointments = appointmentRepository.ListBetween(
            now.Subtract(AppointmentReservation.DefaultProtectionWindow),
            now.Add(AppointmentLookAhead));

        return new PublicDisplaySnapshot(
            now,
            turns,
            barbers,
            appointments,
            GetProtectedBarberIds(appointments, now));
    }

    private static IReadOnlySet<Guid> GetProtectedBarberIds(
        IEnumerable<AppointmentReservation> appointments,
        DateTimeOffset now)
    {
        return appointments
            .Where(appointment => appointment.State is AppointmentState.Confirmed or AppointmentState.ProtectionStarted)
            .Where(appointment => IsWithinProtectionWindow(appointment, now))
            .Select(appointment => appointment.BarberId)
            .ToHashSet();
    }

    private static bool IsWithinProtectionWindow(AppointmentReservation appointment, DateTimeOffset now)
    {
        var protectionStartsAt = appointment.ScheduledFor - appointment.ProtectionWindow;

        return now >= protectionStartsAt && now <= appointment.ScheduledFor;
    }
}

public sealed record PublicDisplaySnapshot(
    DateTimeOffset LoadedAt,
    IReadOnlyList<Turn> ActiveTurns,
    IReadOnlyList<Barber> Barbers,
    IReadOnlyList<AppointmentReservation> Appointments,
    IReadOnlySet<Guid> ProtectedBarberIds);
