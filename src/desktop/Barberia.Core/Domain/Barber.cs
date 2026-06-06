namespace Barberia.Core.Domain;

public sealed record Barber
{
    public Barber(
        Guid id,
        string displayName,
        BarberState state,
        int clientsServedToday,
        int rotationOrder,
        DateTimeOffset? checkedInAt = null,
        int? stationNumber = null,
        string? profileImagePath = null,
        bool isActive = true)
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

        if (stationNumber is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stationNumber), "Station number must be positive.");
        }

        if (isActive && stationNumber is null)
        {
            throw new ArgumentException("Active barbers require a fixed station number.", nameof(stationNumber));
        }

        Id = id;
        DisplayName = displayName.Trim();
        State = state;
        ClientsServedToday = clientsServedToday;
        RotationOrder = rotationOrder;
        CheckedInAt = checkedInAt;
        StationNumber = isActive ? stationNumber : null;
        ProfileImagePath = string.IsNullOrWhiteSpace(profileImagePath) ? null : profileImagePath.Trim();
        IsActive = isActive;
    }

    public Guid Id { get; }

    public string DisplayName { get; }

    public BarberState State { get; }

    public int ClientsServedToday { get; }

    public int RotationOrder { get; }

    public DateTimeOffset? CheckedInAt { get; }

    public int? StationNumber { get; }

    public string? StationCode => StationNumber is null ? null : $"B-{StationNumber.Value}";

    public string DisplayNameWithStation => StationCode is null ? DisplayName : $"{StationCode} - {DisplayName}";

    public string? ProfileImagePath { get; }

    public bool IsActive { get; }
}
