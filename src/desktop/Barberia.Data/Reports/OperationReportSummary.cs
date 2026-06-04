namespace Barberia.Data.Reports;

public sealed record OperationReportSummary(
    int CheckIns,
    int WalkIns,
    int Appointments,
    int CompletedServices,
    int ActiveTurns,
    int NoShows,
    int Cancelled);
