namespace Barberia.Desktop.Services;

public sealed record CashBoxOpeningResult(
    DateOnly BusinessDate,
    decimal OpeningBalance,
    bool WasCorrection);
