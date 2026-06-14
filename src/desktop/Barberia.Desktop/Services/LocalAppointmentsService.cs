using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;

namespace Barberia.Desktop.Services;

public sealed class LocalAppointmentsService
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public LocalAppointmentsService()
        : this(LocalDesktopDatabase.CreateConnectionFactory())
    {
    }

    public LocalAppointmentsService(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        new LocalDatabaseInitializer(_connectionFactory).Initialize();
    }

    public AppointmentsSnapshot Load()
    {
        var now = OperationalClock.Now;
        var businessDate = DailyOperationCoordinator.GetBusinessDate(now);
        var startOfDay = OperationalClock.StartOfDay(businessDate);
        var endOfDay = startOfDay.AddDays(1);

        using var connection = _connectionFactory.OpenConnection();
        var appointmentRepository = new AppointmentReservationRepository(connection);
        var turnRepository = new LocalTurnRepository(connection);
        var barberRepository = new LocalBarberRepository(connection);
        var serviceRepository = new ServiceRepository(connection);

        var appointments = appointmentRepository.ListBetween(startOfDay, endOfDay);
        var barbers = barberRepository.ListAll().ToDictionary(b => b.Id);
        var services = serviceRepository.ListAll().ToDictionary(s => s.Id);

        var items = new List<AppointmentSnapshotItem>();

        foreach (var appointment in appointments)
        {
            var barber = barbers.GetValueOrDefault(appointment.BarberId);
            var service = appointment.ServiceId.HasValue ? services.GetValueOrDefault(appointment.ServiceId.Value) : null;
            var turn = turnRepository.GetByAppointmentId(appointment.Id);

            items.Add(new AppointmentSnapshotItem(
                appointment,
                barber,
                service,
                turn
            ));
        }

        return new AppointmentsSnapshot(now, items);
    }
}

public sealed record AppointmentsSnapshot(
    DateTimeOffset LoadedAt,
    IReadOnlyList<AppointmentSnapshotItem> Items);

public sealed record AppointmentSnapshotItem(
    AppointmentReservation Appointment,
    Barber? Barber,
    Service? Service,
    Turn? LocalTurn);
