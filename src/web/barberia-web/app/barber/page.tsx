/* eslint-disable @typescript-eslint/no-explicit-any */
import { AppShell } from "@/components/layout/app-shell";
import { requireBarber } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";
import { getBarberAppointments } from "@/lib/booking/queries";
import styles from "./barber.module.css";

export default async function BarberPage() {
  const supabase = await createClient();
  await requireBarber(supabase);

  const appointments = await getBarberAppointments(supabase);

  return (
    <AppShell title="Barber's schedule" variant="barber">
      <div className={styles.container}>
        <h1 className={styles.title}>Today&apos;s Schedule</h1>

        {appointments.length === 0 ? (
          <div className={styles.emptyState}>
            <p className={styles.emptyText}>No upcoming appointments found for your profile.</p>
          </div>
        ) : (
          <div className={styles.appointmentList}>
            {appointments.map(app => {
              const date = new Date(app.starts_at);
              let badgeClass = styles.statusDefault;
              if (app.status === 'confirmed') badgeClass = styles.statusConfirmed;
              else if (app.status === 'cancelled') badgeClass = styles.statusCancelled;
              else if (app.status === 'completed') badgeClass = styles.statusCompleted;
              else if (app.status === 'no_show') badgeClass = styles.statusNoShow;

              return (
                <div key={app.id} className={styles.card}>
                  <div className={styles.cardHeader}>
                    <h3 className={styles.cardTitle}>{(app.service as any)?.name}</h3>
                    <span className={`${styles.statusBadge} ${badgeClass}`}>
                      {app.status.replace('_', ' ')}
                    </span>
                  </div>

                  <div className={styles.detailsList}>
                    <div className={styles.detailItem}>
                      <span className={styles.detailLabel}>Customer</span>
                      <span className={styles.detailValue}>{(app.customer as any)?.display_name || "Unknown"}</span>
                    </div>
                    <div className={styles.detailItem}>
                      <span className={styles.detailLabel}>Phone</span>
                      <span className={styles.detailValue}>{(app.customer as any)?.phone || "N/A"}</span>
                    </div>
                    <div className={styles.detailItem}>
                      <span className={styles.detailLabel}>Time</span>
                      <span className={styles.detailValue}>
                        {date.toLocaleDateString()} at {date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                      </span>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </AppShell>
  );
}
