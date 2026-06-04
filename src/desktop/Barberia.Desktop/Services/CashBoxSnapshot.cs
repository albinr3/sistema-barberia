using Barberia.Core.Domain;

namespace Barberia.Desktop.Services;

public sealed record CashBoxSnapshot(DateTimeOffset LoadedAt, IReadOnlyList<Barber> Barbers);
