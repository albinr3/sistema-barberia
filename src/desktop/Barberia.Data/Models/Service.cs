namespace Barberia.Data.Models;

public sealed record Service(
    Guid Id,
    string Name,
    long DesktopPriceCents,
    long WebPriceCents,
    bool IsActive,
    int DisplayOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public Service(
        Guid id,
        string name,
        decimal desktopPrice,
        decimal webPrice,
        bool isActive,
        int displayOrder,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : this(
            id,
            name,
            Money.ToCents(desktopPrice),
            Money.ToCents(webPrice),
            isActive,
            displayOrder,
            createdAt,
            updatedAt)
    {
    }

    public decimal DesktopPrice => Money.FromCents(DesktopPriceCents);
    
    public decimal WebPrice => Money.FromCents(WebPriceCents);

    public string DisplayNameWithPrice => $"{Name} (${DesktopPrice:0.00})";
}
