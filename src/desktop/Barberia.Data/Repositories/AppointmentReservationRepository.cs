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
                id, barber_id, service_id, appointment_code, customer_name, state, scheduled_for, ends_at,
                protection_window_minutes, checked_in_at, no_show_at, completed_at, updated_at
            ) VALUES (
                $id, $barber_id, $service_id, $appointment_code, $customer_name, $state, $scheduled_for, $ends_at,
                $protection_window_minutes, $checked_in_at, $no_show_at, $completed_at, $updated_at
            )
            ON CONFLICT(id) DO UPDATE SET
                barber_id = excluded.barber_id,
                service_id = excluded.service_id,
                appointment_code = excluded.appointment_code,
                customer_name = excluded.customer_name,
                state = excluded.state,
                scheduled_for = excluded.scheduled_for,
                ends_at = excluded.ends_at,
                protection_window_minutes = excluded.protection_window_minutes,
                checked_in_at = excluded.checked_in_at,
                no_show_at = excluded.no_show_at,
                completed_at = excluded.completed_at,
                updated_at = excluded.updated_at;
            """;
        command.AddText("$id", appointment.Id.ToString());
        command.AddText("$barber_id", appointment.BarberId.ToString());
        command.AddText("$service_id", appointment.ServiceId?.ToString());
        command.AddText("$appointment_code", appointment.AppointmentCode);
        command.AddText("$customer_name", appointment.CustomerName);
        command.AddInteger("$state", (int)appointment.State);
        command.AddText("$scheduled_for", appointment.ScheduledFor.ToString("O"));
        command.AddText("$ends_at", appointment.EndsAt.ToString("O"));
        command.AddInteger("$protection_window_minutes", (int)appointment.ProtectionWindow.TotalMinutes);
        command.AddText("$checked_in_at", appointment.CheckedInAt?.ToString("O"));
        command.AddText("$no_show_at", appointment.NoShowAt?.ToString("O"));
        command.AddText("$completed_at", appointment.CompletedAt?.ToString("O"));
        command.AddText("$updated_at", updatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<AppointmentReservation> ListBetween(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, barber_id, service_id, appointment_code, customer_name, state, scheduled_for, ends_at,
                   protection_window_minutes, checked_in_at, no_show_at, completed_at
            FROM appointment_reservations
            WHERE scheduled_for >= $starts_at
              AND scheduled_for <= $ends_at
            ORDER BY scheduled_for;
            """;
        command.AddText("$starts_at", startsAt.ToString("O"));
        command.AddText("$ends_at", endsAt.ToString("O"));

        return ReadAppointments(command);
    }

    public AppointmentReservation? GetById(Guid id)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, barber_id, service_id, appointment_code, customer_name, state, scheduled_for, ends_at,
                   protection_window_minutes, checked_in_at, no_show_at, completed_at
            FROM appointment_reservations
            WHERE id = $id;
            """;
        command.AddText("$id", id.ToString());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadAppointment(reader) : null;
    }

    public AppointmentReservation? GetByAppointmentCode(string appointmentCode)
    {
        if (string.IsNullOrWhiteSpace(appointmentCode))
        {
            return null;
        }

        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, barber_id, service_id, appointment_code, customer_name, state, scheduled_for, ends_at,
                   protection_window_minutes, checked_in_at, no_show_at, completed_at
            FROM appointment_reservations
            WHERE appointment_code = $appointment_code;
            """;
        command.AddText("$appointment_code", appointmentCode.Trim().ToUpperInvariant());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadAppointment(reader) : null;
    }

    public IReadOnlyList<AppointmentReservation> ListDueForNoShow(DateTimeOffset now, TimeSpan gracePeriod)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, barber_id, service_id, appointment_code, customer_name, state, scheduled_for, ends_at,
                   protection_window_minutes, checked_in_at, no_show_at, completed_at
            FROM appointment_reservations
            WHERE state IN ($confirmed, $protection_started)
              AND checked_in_at IS NULL
              AND completed_at IS NULL
              AND scheduled_for <= $deadline
            ORDER BY scheduled_for;
            """;
        command.AddInteger("$confirmed", (int)AppointmentState.Confirmed);
        command.AddInteger("$protection_started", (int)AppointmentState.ProtectionStarted);
        command.AddText("$deadline", now.Subtract(gracePeriod).ToString("O"));

        return ReadAppointments(command);
    }

    public void MarkCheckedIn(Guid appointmentId, DateTimeOffset checkedInAt, DateTimeOffset updatedAt)
    {
        UpdateState(
            appointmentId,
            AppointmentState.CheckedIn,
            "$checked_in_at",
            checkedInAt,
            updatedAt,
            allowedStates: [AppointmentState.Confirmed, AppointmentState.ProtectionStarted, AppointmentState.CheckedIn]);
    }

    public void MarkNoShow(Guid appointmentId, DateTimeOffset noShowAt, DateTimeOffset updatedAt)
    {
        UpdateState(
            appointmentId,
            AppointmentState.NoShow,
            "$no_show_at",
            noShowAt,
            updatedAt,
            allowedStates: [AppointmentState.Confirmed, AppointmentState.ProtectionStarted]);
    }

    public void MarkCompleted(Guid appointmentId, DateTimeOffset completedAt, DateTimeOffset updatedAt)
    {
        UpdateState(
            appointmentId,
            AppointmentState.Completed,
            "$completed_at",
            completedAt,
            updatedAt,
            allowedStates: [AppointmentState.Confirmed, AppointmentState.ProtectionStarted, AppointmentState.CheckedIn, AppointmentState.Completed]);
    }

    private void UpdateState(
        Guid appointmentId,
        AppointmentState state,
        string timestampParameterName,
        DateTimeOffset timestamp,
        DateTimeOffset updatedAt,
        IReadOnlyCollection<AppointmentState> allowedStates)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = $"""
            UPDATE appointment_reservations
            SET state = $state,
                {timestampParameterName.TrimStart('$')} = COALESCE({timestampParameterName.TrimStart('$')}, {timestampParameterName}),
                updated_at = $updated_at
            WHERE id = $id
              AND state IN ({string.Join(", ", allowedStates.Select((_, index) => $"$allowed_{index}"))});
            """;
        command.AddText("$id", appointmentId.ToString());
        command.AddInteger("$state", (int)state);
        command.AddText(timestampParameterName, timestamp.ToString("O"));
        command.AddText("$updated_at", updatedAt.ToString("O"));
        var index = 0;
        foreach (var allowedState in allowedStates)
        {
            command.AddInteger($"$allowed_{index}", (int)allowedState);
            index++;
        }

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("Appointment reservation was not found for state update.");
        }
    }

    private static IReadOnlyList<AppointmentReservation> ReadAppointments(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var appointments = new List<AppointmentReservation>();
        while (reader.Read())
        {
            appointments.Add(ReadAppointment(reader));
        }

        return appointments;
    }

    private static AppointmentReservation ReadAppointment(SqliteDataReader reader)
    {
        return new AppointmentReservation(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            (AppointmentState)reader.GetInt32(5),
            DateTimeOffset.Parse(reader.GetString(6)),
            TimeSpan.FromMinutes(reader.GetInt32(8)),
            reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)),
            reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9)),
            reader.IsDBNull(10) ? null : DateTimeOffset.Parse(reader.GetString(10)),
            reader.IsDBNull(11) ? null : DateTimeOffset.Parse(reader.GetString(11)));
    }
}
