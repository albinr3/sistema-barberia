namespace Barberia.Data.Models;

public sealed record Service(
    Guid Id,
    string Name,
    long PriceCents,
    bool IsActive,
    int DisplayOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public Service(
        Guid id,
        string name,
        decimal price,
        bool isActive,
        int displayOrder,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : this(
            id,
            name,
            Money.ToCents(price),
            isActive,
            displayOrder,
            createdAt,
            updatedAt)
    {
    }

    public decimal Price => Money.FromCents(PriceCents);

    public string DisplayNameWithPrice => $"{Name} (${Price:0.00})";
}
