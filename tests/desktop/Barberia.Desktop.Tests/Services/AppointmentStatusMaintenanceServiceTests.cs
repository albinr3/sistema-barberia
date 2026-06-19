using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Barberia.Data.Sync;
using Barberia.Desktop.Services;
using Xunit;

namespace Barberia.Desktop.Tests.Services;

public sealed class AppointmentStatusMaintenanceServiceTests
{
    [Fact]
    public void ApplyDueNoShows_MarksOverdueAppointmentAndEnqueuesSyncEvent()
    {
        using var database = TestDatabase.Create();
        var scheduledFor = new DateTimeOffset(2026, 6, 18, 8, 30, 0, TimeSpan.FromHours(-4));
        var now = new DateTimeOffset(2026, 6, 18, 9, 8, 0, TimeSpan.FromHours(-4));
        var barberId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            new LocalBarberRepository(connection).Upsert(
                new Barber(barberId, "Ana", BarberState.Available, 0, 0, scheduledFor, stationNumber: 1),
                scheduledFor);
            new AppointmentReservationRepository(connection).Upsert(
                new AppointmentReservation(
                    appointmentId,
                    barberId,
                    AppointmentState.Confirmed,
                    scheduledFor,
                    AppointmentReservation.DefaultProtectionWindow,
                    appointmentCode: "A123456789ABC",
                    customerName: "Cliente prueba"),
                scheduledFor);
        }

        AppointmentStatusMaintenanceService.ApplyDueNoShows(database.ConnectionFactory, now, "desktop-test");

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            var appointment = new AppointmentReservationRepository(connection).GetById(appointmentId);
            Assert.NotNull(appointment);
            Assert.Equal(AppointmentState.NoShow, appointment!.State);
            Assert.Equal(now, appointment.NoShowAt);

            var outboxEvents = new SyncOutboxRepository(connection).ListAll();
            var noShowEvent = Assert.Single(outboxEvents, e => e.EventType == "appointment.no_show" && e.AggregateId == appointmentId);
            Assert.Contains("A123456789ABC", noShowEvent.Payload);
        }
    }

    [Fact]
    public void ApplyDueNoShows_DoesNotMarkAppointmentWithExistingLocalTurn()
    {
        using var database = TestDatabase.Create();
        var scheduledFor = new DateTimeOffset(2026, 6, 18, 8, 30, 0, TimeSpan.FromHours(-4));
        var now = new DateTimeOffset(2026, 6, 18, 9, 8, 0, TimeSpan.FromHours(-4));
        var barberId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            new LocalBarberRepository(connection).Upsert(
                new Barber(barberId, "Ana", BarberState.InService, 0, 0, scheduledFor, stationNumber: 1),
                scheduledFor);
            new AppointmentReservationRepository(connection).Upsert(
                new AppointmentReservation(
                    appointmentId,
                    barberId,
                    AppointmentState.Confirmed,
                    scheduledFor,
                    AppointmentReservation.DefaultProtectionWindow,
                    appointmentCode: "A123456789ABC",
                    customerName: "Cliente prueba"),
                scheduledFor);
            new LocalTurnRepository(connection).Upsert(
                new Turn(
                    Guid.NewGuid(),
                    "A123456789ABC",
                    1,
                    DateOnly.FromDateTime(scheduledFor.Date),
                    TurnState.InService,
                    TurnSource.Appointment,
                    scheduledFor,
                    barberId,
                    appointmentId),
                scheduledFor);
        }

        AppointmentStatusMaintenanceService.ApplyDueNoShows(database.ConnectionFactory, now, "desktop-test");

        using (var connection = database.ConnectionFactory.OpenConnection())
        {
            var appointment = new AppointmentReservationRepository(connection).GetById(appointmentId);
            Assert.NotNull(appointment);
            Assert.Equal(AppointmentState.Confirmed, appointment!.State);
            Assert.Null(appointment.NoShowAt);
            Assert.Empty(new SyncOutboxRepository(connection).ListAll());
        }
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
