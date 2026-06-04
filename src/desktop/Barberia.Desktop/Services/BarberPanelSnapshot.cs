using Barberia.Core.Domain;

namespace Barberia.Desktop.Services;

public sealed record BarberPanelSnapshot(
    DateTimeOffset LoadedAt,
    IReadOnlyList<Barber> Barbers,
    IReadOnlyList<Turn> AssignedTurns);
