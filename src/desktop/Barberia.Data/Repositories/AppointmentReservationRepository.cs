using Barberia.Core.Domain;
using Microsoft.Data.Sqlite;

namespace Barberia.Data.Repositories;

public sealed class AppointmentReservationRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;

    public AppointmentReservationRepository(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public void Upsert(AppointmentReservation appointment, DateTimeOffset updatedAt)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            INSERT INTO appointment_reservations (
                id, barber_id, state, scheduled_for, protection_window_minutes, updated_at
            ) VALUES (
                $id, $barber_id, $state, $scheduled_for, $protection_window_minutes, $updated_at
            )
            ON CONFLICT(id) DO UPDATE SET
                barber_id = excluded.barber_id,
                state = excluded.state,
                scheduled_for = excluded.scheduled_for,
                protection_window_minutes = excluded.protection_window_minutes,
                updated_at = excluded.updated_at;
            """;
        command.AddText("$id", appointment.Id.ToString());
        command.AddText("$barber_id", appointment.BarberId.ToString());
        command.AddInteger("$state", (int)appointment.State);
        command.AddText("$scheduled_for", appointment.ScheduledFor.ToString("O"));
        command.AddInteger("$protection_window_minutes", (int)appointment.ProtectionWindow.TotalMinutes);
        command.AddText("$updated_at", updatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<AppointmentReservation> ListBetween(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, barber_id, state, scheduled_for, protection_window_minutes
            FROM appointment_reservations
            WHERE scheduled_for >= $starts_at
              AND scheduled_for <= $ends_at
            ORDER BY scheduled_for;
            """;
        command.AddText("$starts_at", startsAt.ToString("O"));
        command.AddText("$ends_at", endsAt.ToString("O"));

        using var reader = command.ExecuteReader();
        var appointments = new List<AppointmentReservation>();
        while (reader.Read())
        {
            appointments.Add(new AppointmentReservation(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                (AppointmentState)reader.GetInt32(2),
                DateTimeOffset.Parse(reader.GetString(3)),
                TimeSpan.FromMinutes(reader.GetInt32(4))));
        }

        return appointments;
    }
}
