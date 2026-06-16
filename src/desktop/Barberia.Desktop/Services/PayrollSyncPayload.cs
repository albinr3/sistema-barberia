using System.Text.Json;
using Barberia.Data.Models;

namespace Barberia.Desktop.Services;

internal static class PayrollSyncPayload
{
    public static string CreateSnapshot(PayrollSnapshot snapshot)
    {
        return JsonSerializer.Serialize(new
        {
            period = new
            {
                id = snapshot.Period.Id,
                start_date = snapshot.Period.StartDate.ToString("yyyy-MM-dd"),
                end_date = snapshot.Period.EndDate.ToString("yyyy-MM-dd"),
                state = ToState(snapshot.Period.State),
                total_services = snapshot.Period.TotalServices,
                total_commission_cents = snapshot.Period.TotalCommissionCents,
                total_adjustments_cents = snapshot.Period.TotalAdjustmentsCents,
                total_to_pay_cents = snapshot.Period.TotalToPayCents,
                payment_method = snapshot.Period.PaymentMethod?.ToString().ToLowerInvariant(),
                payment_reference = snapshot.Period.PaymentReference,
                notes = snapshot.Period.Notes,
                generated_at = snapshot.Period.GeneratedAt.ToString("O"),
                paid_at = snapshot.Period.PaidAt?.ToString("O")
            },
            lines = snapshot.Lines.Select(line => new
            {
                id = line.Id,
                barber_id = line.BarberId,
                barber_name = line.BarberName,
                station_number = line.StationNumber,
                closed_services_count = line.ClosedServicesCount,
                sales_generated_cents = line.SalesGeneratedCents,
                commission_cents = line.CommissionCents,
                adjustments_cents = line.AdjustmentsCents,
                total_cents = line.TotalCents
            }),
            adjustments = snapshot.Adjustments.Select(adjustment => new
            {
                id = adjustment.Id,
                barber_id = adjustment.BarberId,
                amount_cents = adjustment.AmountCents,
                reason = adjustment.Reason,
                created_at = adjustment.CreatedAt.ToString("O")
            }),
            loaded_at = snapshot.LoadedAt.ToString("O")
        });
    }

    public static string CreateCommandAck(Guid commandId, bool success, string? errorMessage)
    {
        return JsonSerializer.Serialize(new
        {
            command_id = commandId,
            status = success ? "applied" : "failed",
            error_message = errorMessage
        });
    }

    public static string CreateHeartbeat(int pendingOutboxCount, DateTimeOffset measuredAt)
    {
        return JsonSerializer.Serialize(new
        {
            pending_outbox_count = pendingOutboxCount,
            measured_at = measuredAt.ToString("O")
        });
    }

    private static string ToState(PayrollPeriodState state)
    {
        return state == PayrollPeriodState.Paid ? "paid" : "draft";
    }
}
