using Barberia.Core.Domain;
using Barberia.Data.Models;
using Barberia.Data.Reports;

namespace Barberia.Desktop.Services;

public sealed record LocalAdminSnapshot(
    DateTimeOffset GeneratedAt,
    string DatabasePath,
    long DatabaseSizeBytes,
    OperationReportSummary Operations,
    CashReportSummary Cash,
    IReadOnlyList<Barber> Barbers,
    IReadOnlyList<Service> Services,
    IReadOnlyList<ProfileImageOption> ProfileImages,
    IReadOnlyList<Turn> ActiveTurns,
    IReadOnlyList<LocalAdminAlert> Alerts,
    IReadOnlyList<AuditEvent> RecentAuditEvents,
    IReadOnlyList<TicketHistoryRow> RecentTicketHistoryToday);

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

public sealed record LocalAdminAlert(
    AlertSeverity Severity,
    string Title,
    string Detail,
    int? DisplayTicketNumber,
    Guid? TurnId,
    Guid? BarberId,
    int ElapsedMinutes);

public sealed record ProfileImageOption(string DisplayName, string? RelativePath);
