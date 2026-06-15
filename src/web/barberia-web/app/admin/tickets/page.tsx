import { requireAdmin } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";
import { getTicketsDashboardSnapshot } from "@/lib/tickets-dashboard";
import { ReassignForm } from "./reassign-form";
import styles from "./tickets.module.css";
import { formatTicketNumber } from "@/lib/tickets-dashboard";
import { AppShell } from "@/components/layout/app-shell";

export const dynamic = "force-dynamic";

export default async function AdminTicketsPage() {
  const supabase = await createClient();
  await requireAdmin(supabase);
  const snapshot = await getTicketsDashboardSnapshot(supabase);

  const activeTickets = [...snapshot.nowCalling, ...snapshot.waiting];

  return (
    <AppShell title="Ticket Operations" variant="admin">
      <div className={styles.container}>
        <header className={styles.header}>
          <p>Reassign waiting or calling tickets to specific barbers.</p>
        </header>

        {activeTickets.length === 0 ? (
          <div className={styles.empty}>No active tickets available for reassignment.</div>
        ) : (
          <div className={styles.ticketGrid}>
            {activeTickets.map(ticket => (
              <article key={ticket.id} className={styles.ticketCard}>
                <div className={styles.ticketHeader}>
                  <strong>Ticket #{formatTicketNumber(ticket)}</strong>
                  <span className={styles.statusBadge}>{ticket.status}</span>
                </div>
                <div className={styles.ticketInfo}>
                  <p><strong>Customer:</strong> {ticket.customer_name || "Walk-in"}</p>
                  <p><strong>Current Barber:</strong> {ticket.barber?.display_name || "Any available"}</p>
                </div>
                <div className={styles.reassignSection}>
                  <ReassignForm ticket={ticket} barbers={snapshot.barbers} />
                </div>
              </article>
            ))}
          </div>
        )}
      </div>
    </AppShell>
  );
}
