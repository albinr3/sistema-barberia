"use client";

import { useActionState } from "react";
import { adminReassignTicket } from "@/app/actions/admin-tickets";
import styles from "./tickets.module.css";
import type { TicketDashboardBarberRow, TicketDashboardTicketRow } from "@/lib/tickets-dashboard";

type ReassignState = {
  error?: string;
  success?: boolean;
  commandId?: string;
} | null;

export function ReassignForm({ 
  ticket, 
  barbers 
}: { 
  ticket: TicketDashboardTicketRow; 
  barbers: TicketDashboardBarberRow[]; 
}) {
  const [state, formAction, isPending] = useActionState<ReassignState, FormData>(async (_previousState, formData) => {
    const targetBarberId = formData.get("targetBarberId") as string;
    if (!targetBarberId) return { error: "Select a barber" };
    return adminReassignTicket(ticket.id, targetBarberId);
  }, null);

  const availableBarbers = barbers.filter(b => b.is_active && b.is_available_locally !== false);

  return (
    <form action={formAction} className={styles.reassignForm}>
      <div className={styles.formRow}>
        <select name="targetBarberId" defaultValue={ticket.barber_id ?? ""} required disabled={isPending}>
          <option value="" disabled>Select target barber...</option>
          {availableBarbers.map(barber => (
            <option key={barber.id} value={barber.id} disabled={barber.id === ticket.barber_id}>
              {barber.display_name} ({barber.station_code ?? "No station"})
            </option>
          ))}
        </select>
        <button type="submit" disabled={isPending}>
          {isPending ? "Reassigning..." : "Reassign"}
        </button>
      </div>
      {state?.error && <div className={styles.error}>{state.error}</div>}
      {state?.success && <div className={styles.success}>Request sent</div>}
    </form>
  );
}
