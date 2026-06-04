namespace Barberia.Core.Assignment;

public sealed record CashBoxCloseRequest
{
    public CashBoxCloseRequest(Guid barberId, IEnumerable<Guid> rotationQueue)
    {
        if (barberId == Guid.Empty)
        {
            throw new ArgumentException("Barber id cannot be empty.", nameof(barberId));
        }

        BarberId = barberId;
        RotationQueue = rotationQueue?.ToArray() ?? throw new ArgumentNullException(nameof(rotationQueue));
    }

    public Guid BarberId { get; }

    public IReadOnlyList<Guid> RotationQueue { get; }
}
