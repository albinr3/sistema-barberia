using Barberia.Data.Models;

namespace Barberia.Desktop.Services;

public sealed record CashBoxSnapshot(
    DateTimeOffset LoadedAt,
    IReadOnlyList<Service> Services,
    int PendingPaymentCount,
    bool IsCashBoxOpened,
    decimal OpeningBalance,
    decimal CashCollected,
    decimal ZelleCollected,
    decimal CashInDrawer,
    string Currency);
