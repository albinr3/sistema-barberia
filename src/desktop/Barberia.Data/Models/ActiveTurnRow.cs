using Barberia.Core.Domain;

namespace Barberia.Data.Models;

public sealed record ActiveTurnRow(
    Turn Turn,
    DateTimeOffset UpdatedAt);
