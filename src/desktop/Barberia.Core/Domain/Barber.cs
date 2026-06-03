namespace Barberia.Core.Domain;

public sealed record Barber
{
    public Barber(
        Guid id,
        string displayName,
        BarberState state,
        int clientsServedToday,
        int rotationOrder,
        DateTimeOffset? checkedInAt = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Barber id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Barber display name is required.", nameof(displayName));
        }

        if (clientsServedToday < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(clientsServedToday), "Clients served today cannot be negative.");
        }

        if (rotationOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rotationOrder), "Rotation order cannot be negative.");
        }

        Id = id;
        DisplayName = displayName.Trim();
        State = state;
        ClientsServedToday = clientsServedToday;
        RotationOrder = rotationOrder;
        CheckedInAt = checkedInAt;
    }

    public Guid Id { get; }

    public string DisplayName { get; }

    public BarberState State { get; }

    public int ClientsServedToday { get; }

    public int RotationOrder { get; }

    public DateTimeOffset? CheckedInAt { get; }
}
