using System.Text.Json;
using Barberia.Data;
using Barberia.Data.Repositories;
using Barberia.Data.Sync;
using Barberia.Sync.Outbox;

namespace Barberia.Desktop.Services;

internal static class AppointmentStatusMaintenanceService
{
    private static readonly TimeSpan NoShowGracePeriod = TimeSpan.FromMinutes(10);

    public static void ApplyDueNoShows(SqliteConnectionFactory connectionFactory, DateTimeOffset now, string sourceDeviceId)
    {
        var transaction = new LocalDataTransaction(connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            var appointmentRepository = new AppointmentReservationRepository(connection, sqliteTransaction);
            var turnRepository = new LocalTurnRepository(connection, sqliteTransaction);
            var syncRecorder = new SyncOutboxRecorder(new SyncOutboxRepository(connection, sqliteTransaction));

            foreach (var appointment in appointmentRepository.ListDueForNoShow(now, NoShowGracePeriod))
            {
                if (turnRepository.GetByAppointmentId(appointment.Id) is not null)
                {
                    continue;
                }

                appointmentRepository.MarkNoShow(appointment.Id, now, now);
                syncRecorder.Enqueue(new LocalSyncEvent(
                    Guid.NewGuid(),
                    now,
                    "appointment.no_show",
                    "appointment",
                    appointment.Id,
                    JsonSerializer.Serialize(new
                    {
                        appointment_id = appointment.Id,
                        appointment_code = appointment.AppointmentCode,
                        no_show_at = now
                    }),
                    sourceDeviceId), now);
            }
        });
    }
}
