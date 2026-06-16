import { requireAdmin } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";
import { getTicketsDashboardSnapshot } from "@/lib/tickets-dashboard";
import { ReassignForm } from "./reassign-form";
import { CancelForm } from "./cancel-form";
import styles from "./tickets.module.css";
import { formatTicketNumber } from "@/lib/tickets-dashboard";
import { AppShell } from "@/components/layout/app-shell";

export const dynamic = "force-dynamic";

export default async function AdminTicketsPage() {
  const supabase = await createClient();
  await requireAdmin(supabase);
  const snapshot = await getTicketsDashboardSnapshot(supabase);

  const activeQueue = snapshot.activeQueue;

  return (
    <AppShell title="Active Queue Monitor" variant="admin">
      <div className={styles.container}>
        <header className={styles.header}>
          <p>Monitor the active queue. Cancel or reassign tickets as needed.</p>
        </header>

        {activeQueue.length === 0 ? (
          <div className={styles.empty}>No active tickets in the queue.</div>
        ) : (
          <div className={styles.tableContainer}>
            <table className={styles.queueTable}>
              <thead>
                <tr>
                  <th>Ticket</th>
                  <th>Customer</th>
                  <th>Status</th>
                  <th>Assigned To</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {activeQueue.map((ticket) => (
                  <tr key={ticket.id}>
                    <td><strong>#{formatTicketNumber(ticket)}</strong></td>
                    <td>{ticket.customer_name || "Walk-in"}</td>
                    <td><span className={styles.statusBadge}>{ticket.status}</span></td>
                    <td>{ticket.barber?.display_name || "Any available"}</td>
                    <td className={styles.actionsCell}>
                      {["waiting", "called"].includes(ticket.status) && (
                        <ReassignForm ticket={ticket} barbers={snapshot.barbers} />
                      )}
                      <CancelForm ticket={ticket} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </AppShell>
  );
}
