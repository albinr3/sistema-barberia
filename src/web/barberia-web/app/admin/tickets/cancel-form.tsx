"use client";

import { useActionState } from "react";
import { adminCancelTicket } from "@/app/actions/admin-tickets";
import styles from "./tickets.module.css";
import type { TicketDashboardTicketRow } from "@/lib/tickets-dashboard";

export function CancelForm({ 
  ticket 
}: { 
  ticket: TicketDashboardTicketRow; 
}) {
  const [state, formAction, isPending] = useActionState(async () => {
    return adminCancelTicket(ticket.id);
  }, null);

  return (
    <form 
      action={formAction} 
      className={styles.cancelForm}
      onSubmit={(e) => {
        if (!confirm("Are you sure you want to cancel this ticket?")) {
          e.preventDefault();
        }
      }}
    >
      <button 
        type="submit" 
        disabled={isPending} 
        className={styles.cancelButton}
      >
        {isPending ? "Cancelling..." : "Cancel Ticket"}
      </button>
      {state?.error && <div className={styles.error}>{state.error}</div>}
      {state?.success && <div className={styles.success}>Request sent</div>}
    </form>
  );
}
