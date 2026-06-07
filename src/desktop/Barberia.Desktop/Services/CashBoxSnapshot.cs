using Barberia.Data.Models;

namespace Barberia.Desktop.Services;

public sealed record CashBoxSnapshot(
    DateTimeOffset LoadedAt,
    IReadOnlyList<Service> Services);
