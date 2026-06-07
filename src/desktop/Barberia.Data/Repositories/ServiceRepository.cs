using Barberia.Data.Models;
using Microsoft.Data.Sqlite;

namespace Barberia.Data.Repositories;

public sealed class ServiceRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;

    public ServiceRepository(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public IReadOnlyList<Service> ListActive()
    {
        using var command = CreateCommand();
        command.CommandText = """
            SELECT id, name, price_cents, is_active, display_order, created_at, updated_at
            FROM services
            WHERE is_active = 1
            ORDER BY display_order, name, id;
            """;

        return ReadServices(command);
    }

    public IReadOnlyList<Service> ListAll()
    {
        using var command = CreateCommand();
        command.CommandText = """
            SELECT id, name, price_cents, is_active, display_order, created_at, updated_at
            FROM services
            ORDER BY display_order, name, id;
            """;

        return ReadServices(command);
    }

    public Service? GetById(Guid id)
    {
        using var command = CreateCommand();
        command.CommandText = """
            SELECT id, name, price_cents, is_active, display_order, created_at, updated_at
            FROM services
            WHERE id = $id;
            """;
        command.AddText("$id", id.ToString());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadService(reader) : null;
    }

    public void Add(Service service)
    {
        Validate(service);

        using var command = CreateCommand();
        command.CommandText = """
            INSERT INTO services (
                id, name, price_cents, is_active, display_order, created_at, updated_at
            ) VALUES (
                $id, $name, $price_cents, $is_active, $display_order, $created_at, $updated_at
            );
            """;
        AddServiceParameters(command, service);
        command.ExecuteNonQuery();
    }

    public void Update(Service service)
    {
        Validate(service);

        using var command = CreateCommand();
        command.CommandText = """
            UPDATE services
            SET name = $name,
                price_cents = $price_cents,
                is_active = $is_active,
                display_order = $display_order,
                updated_at = $updated_at
            WHERE id = $id;
            """;
        AddServiceParameters(command, service);
        command.ExecuteNonQuery();
    }

    public void SetActive(Guid id, bool isActive, DateTimeOffset updatedAt)
    {
        using var command = CreateCommand();
        command.CommandText = """
            UPDATE services
            SET is_active = $is_active,
                updated_at = $updated_at
            WHERE id = $id;
            """;
        command.AddText("$id", id.ToString());
        command.AddInteger("$is_active", isActive ? 1 : 0);
        command.AddText("$updated_at", updatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void Delete(Guid id)
    {
        using var command = CreateCommand();
        command.CommandText = "DELETE FROM services WHERE id = $id;";
        command.AddText("$id", id.ToString());
        command.ExecuteNonQuery();
    }

    private SqliteCommand CreateCommand()
    {
        var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        return command;
    }

    private static void AddServiceParameters(SqliteCommand command, Service service)
    {
        command.AddText("$id", service.Id.ToString());
        command.AddText("$name", service.Name);
        command.AddInteger("$price_cents", service.PriceCents);
        command.AddInteger("$is_active", service.IsActive ? 1 : 0);
        command.AddInteger("$display_order", service.DisplayOrder);
        command.AddText("$created_at", service.CreatedAt.ToString("O"));
        command.AddText("$updated_at", service.UpdatedAt.ToString("O"));
    }

    private static IReadOnlyList<Service> ReadServices(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var services = new List<Service>();
        while (reader.Read())
        {
            services.Add(ReadService(reader));
        }

        return services;
    }

    private static Service ReadService(SqliteDataReader reader)
    {
        return new Service(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetInt64(2),
            reader.GetInt32(3) == 1,
            reader.GetInt32(4),
            DateTimeOffset.Parse(reader.GetString(5)),
            DateTimeOffset.Parse(reader.GetString(6)));
    }

    private static void Validate(Service service)
    {
        if (service.Id == Guid.Empty)
        {
            throw new ArgumentException("Service id is required.", nameof(service));
        }

        if (string.IsNullOrWhiteSpace(service.Name))
        {
            throw new ArgumentException("Service name is required.", nameof(service));
        }

        if (service.PriceCents <= 0)
        {
            throw new ArgumentException("Service price must be greater than zero.", nameof(service));
        }

        if (service.DisplayOrder < 0)
        {
            throw new ArgumentException("Service display order must be zero or greater.", nameof(service));
        }
    }
}
