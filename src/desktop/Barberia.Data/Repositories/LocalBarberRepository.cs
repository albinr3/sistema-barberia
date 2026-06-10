using Barberia.Core.Domain;
using Microsoft.Data.Sqlite;

namespace Barberia.Data.Repositories;

public sealed class LocalBarberRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;

    public LocalBarberRepository(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public void Upsert(Barber barber, DateTimeOffset updatedAt)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            INSERT INTO barbers (
                id, display_name, state, clients_served_today, rotation_order, station_number, checked_in_at, profile_image_path, is_active, commission_percentage, updated_at
            ) VALUES (
                $id, $display_name, $state, $clients_served_today, $rotation_order, $station_number, $checked_in_at, $profile_image_path, $is_active, $commission_percentage, $updated_at
            )
            ON CONFLICT(id) DO UPDATE SET
                display_name = excluded.display_name,
                state = excluded.state,
                clients_served_today = excluded.clients_served_today,
                rotation_order = excluded.rotation_order,
                station_number = excluded.station_number,
                checked_in_at = excluded.checked_in_at,
                profile_image_path = excluded.profile_image_path,
                is_active = excluded.is_active,
                commission_percentage = excluded.commission_percentage,
                updated_at = excluded.updated_at;
            """;
        command.AddText("$id", barber.Id.ToString());
        command.AddText("$display_name", barber.DisplayName);
        command.AddInteger("$state", (int)barber.State);
        command.AddInteger("$clients_served_today", barber.ClientsServedToday);
        command.AddInteger("$rotation_order", barber.RotationOrder);
        command.AddNullableInteger("$station_number", barber.StationNumber);
        command.AddText("$checked_in_at", Format(barber.CheckedInAt));
        command.AddText("$profile_image_path", barber.ProfileImagePath);
        command.AddInteger("$is_active", barber.IsActive ? 1 : 0);
        command.AddInteger("$commission_percentage", barber.CommissionPercentage);
        command.AddText("$updated_at", Format(updatedAt));
        command.ExecuteNonQuery();
    }

    public Barber? GetById(Guid id)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, display_name, state, clients_served_today, rotation_order, station_number, checked_in_at, profile_image_path, is_active, commission_percentage
            FROM barbers
            WHERE id = $id;
            """;
        command.AddText("$id", id.ToString());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadBarber(reader) : null;
    }

    public IReadOnlyList<Barber> ListAll()
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            SELECT id, display_name, state, clients_served_today, rotation_order, station_number, checked_in_at, profile_image_path, is_active, commission_percentage
            FROM barbers
            ORDER BY rotation_order, display_name;
            """;

        using var reader = command.ExecuteReader();
        var barbers = new List<Barber>();
        while (reader.Read())
        {
            barbers.Add(ReadBarber(reader));
        }

        return barbers;
    }

    public void ApplyAssignment(Guid barberId, BarberState state, DateTimeOffset updatedAt)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            UPDATE barbers
            SET state = $state,
                updated_at = $updated_at
            WHERE id = $id;
            """;
        command.AddText("$id", barberId.ToString());
        command.AddInteger("$state", (int)state);
        command.AddText("$updated_at", Format(updatedAt));

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("Barber was not found for assignment.");
        }
    }

    public void Delete(Guid barberId)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            DELETE FROM barbers
            WHERE id = $id;
            """;
        command.AddText("$id", barberId.ToString());

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("Barber was not found for delete.");
        }
    }

    public void SetActive(Guid barberId, bool isActive, DateTimeOffset updatedAt, int? stationNumber = null)
    {
        if (stationNumber is <= 0)
        {
            throw new InvalidOperationException("Station number must be positive.");
        }

        if (isActive && stationNumber is null)
        {
            throw new InvalidOperationException("Active barbers require a fixed station number.");
        }

        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            UPDATE barbers
            SET is_active = $is_active,
                station_number = $station_number,
                updated_at = $updated_at
            WHERE id = $id;
            """;
        command.AddText("$id", barberId.ToString());
        command.AddInteger("$is_active", isActive ? 1 : 0);
        command.AddNullableInteger("$station_number", isActive ? stationNumber : null);
        command.AddText("$updated_at", Format(updatedAt));

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("Barber was not found for active update.");
        }
    }

    public void SetState(Guid barberId, BarberState state, DateTimeOffset updatedAt)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            UPDATE barbers
            SET state = $state,
                updated_at = $updated_at
            WHERE id = $id;
            """;
        command.AddText("$id", barberId.ToString());
        command.AddInteger("$state", (int)state);
        command.AddText("$updated_at", Format(updatedAt));

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("Barber was not found for state update.");
        }
    }

    public void ApplyCashBoxClose(Guid barberId, BarberState state, int rotationOrder, DateTimeOffset updatedAt)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            UPDATE barbers
            SET state = $state,
                clients_served_today = clients_served_today + 1,
                rotation_order = $rotation_order,
                updated_at = $updated_at
            WHERE id = $id;
            """;
        command.AddText("$id", barberId.ToString());
        command.AddInteger("$state", (int)state);
        command.AddInteger("$rotation_order", rotationOrder);
        command.AddText("$updated_at", Format(updatedAt));

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("Barber was not found for cash box close.");
        }
    }

    public void SetRotationOrder(Guid barberId, int rotationOrder, DateTimeOffset updatedAt)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = """
            UPDATE barbers
            SET rotation_order = $rotation_order,
                updated_at = $updated_at
            WHERE id = $id;
            """;
        command.AddText("$id", barberId.ToString());
        command.AddInteger("$rotation_order", rotationOrder);
        command.AddText("$updated_at", Format(updatedAt));

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("Barber was not found for rotation update.");
        }
    }

    private static Barber ReadBarber(SqliteDataReader reader)
    {
        return new Barber(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            (BarberState)reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6)),
            reader.IsDBNull(5) ? null : reader.GetInt32(5),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetInt32(8) == 1,
            reader.GetInt32(9));
    }

    private static string? Format(DateTimeOffset? value)
    {
        return value?.ToString("O");
    }
}
