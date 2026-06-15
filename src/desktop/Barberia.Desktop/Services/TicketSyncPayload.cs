using Barberia.Core.Domain;

namespace Barberia.Desktop.Services;

internal static class TicketSyncPayload
{
    public static object Create(Turn turn, string status, Guid? barberId = null, DateTimeOffset? occurredAt = null, object? items = null)
    {
        return new
        {
            display_ticket_number = turn.DisplayTicketNumber,
            ticket_date = turn.TicketDate.ToString("yyyy-MM-dd"),
            customer_name = turn.CustomerName,
            assigned_barber_id = turn.AssignedBarberId,
            barber_id = barberId ?? turn.AssignedBarberId,
            appointment_id = turn.AppointmentId,
            status,
            checked_in_at = turn.CheckedInAt,
            started_at = status == "in_progress" ? occurredAt ?? turn.StartedAt : turn.StartedAt,
            completed_at = status == "completed" ? occurredAt ?? turn.CompletedAt : turn.CompletedAt,
            cancelled_at = status == "cancelled" ? occurredAt ?? turn.CancelledAt : turn.CancelledAt,
            items
        };
    }
}
