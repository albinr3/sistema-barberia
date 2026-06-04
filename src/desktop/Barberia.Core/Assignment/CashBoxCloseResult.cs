using Barberia.Core.Domain;

namespace Barberia.Core.Assignment;

public sealed record CashBoxCloseResult(
    Guid BarberId,
    BarberState BarberState,
    IReadOnlyList<Guid> RotationQueue);
