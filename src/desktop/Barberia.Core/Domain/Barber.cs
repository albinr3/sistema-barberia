namespace Barberia.Core.Domain;

public sealed record Barber
{
    public const int DefaultCommissionPercentage = 65;

    public Barber(
        Guid id,
        string displayName,
        BarberState state,
        int clientsServedToday,
        int rotationOrder,
        DateTimeOffset? checkedInAt = null,
        int? stationNumber = null,
        string? profileImagePath = null,
        bool isActive = true,
        int commissionPercentage = DefaultCommissionPercentage,
        DateTimeOffset? updatedAt = null)
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

        if (commissionPercentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(commissionPercentage), "Commission percentage must be between 0 and 100.");
        }

        Id = id;
        DisplayName = displayName.Trim();
        State = state;
        ClientsServedToday = clientsServedToday;
        RotationOrder = rotationOrder;
        CheckedInAt = checkedInAt;
        StationNumber = stationNumber;
        ProfileImagePath = string.IsNullOrWhiteSpace(profileImagePath) ? null : profileImagePath.Trim();
        IsActive = isActive;
        CommissionPercentage = commissionPercentage;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
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

    public int CommissionPercentage { get; }

    public decimal CommissionRate => CommissionPercentage / 100m;

    public DateTimeOffset UpdatedAt { get; }
}
