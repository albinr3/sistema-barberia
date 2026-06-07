using Barberia.Core.Domain;
using Barberia.Data.Models;

namespace Barberia.Desktop.Services;

public sealed record CashBoxSnapshot(
    DateTimeOffset LoadedAt,
    IReadOnlyList<Barber> Barbers,
    IReadOnlyList<Service> Services);
