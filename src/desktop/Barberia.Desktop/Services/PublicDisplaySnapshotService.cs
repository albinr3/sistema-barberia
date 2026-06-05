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
        var now = DateTimeOffset.Now;

        using var connection = _connectionFactory.OpenConnection();
        var turnRepository = new LocalTurnRepository(connection);
        var barberRepository = new LocalBarberRepository(connection);
        var appointmentRepository = new AppointmentReservationRepository(connection);

        var turns = turnRepository.ListActiveForPublicDisplay();
        var barbers = barberRepository
            .ListAll()
            .Where(barber => barber.IsActive)
            .ToArray();
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
