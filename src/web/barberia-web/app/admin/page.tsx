/* eslint-disable @typescript-eslint/no-explicit-any */
import { AppShell } from "@/components/layout/app-shell";
import { requireAdmin } from "@/lib/auth/profile";
import { getAdminDashboardStats } from "@/lib/booking/queries";
import { getTicketsDashboardSnapshot } from "@/lib/tickets-dashboard";
import { createClient } from "@/lib/supabase/server";
import Link from "next/link";
import styles from "./dashboard.module.css";

export default async function AdminPage() {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const [stats, snapshot] = await Promise.all([
    getAdminDashboardStats(supabase),
    getTicketsDashboardSnapshot(supabase)
  ]);

  const todayStr = new Date().toISOString().split("T")[0];

  const todayAppointments = stats.appointments.filter((a) => a.starts_at.startsWith(todayStr));
  const upcomingAppointments = stats.appointments.filter((a) => a.starts_at > todayStr && !a.starts_at.startsWith(todayStr) && a.status !== "cancelled" && a.status !== "no_show");
  const recentNoShows = stats.appointments.filter((a) => a.status === "no_show");

  return (
    <AppShell title="Admin dashboard" variant="admin">
      <div className={styles.page}>
        <header className={styles.pageHeader}>
          <h1 className={styles.pageTitle}>Operational Summary</h1>
        </header>

        {snapshot.alerts.length > 0 && (
          <section className={styles.alertsSection}>
            <div className={styles.panelHeader}>
              <h2 className={`${styles.panelTitle} ${styles.metricValueDanger}`}>Active Alerts</h2>
            </div>
            <ul className={styles.list}>
              {snapshot.alerts.map((alert, index) => (
                <li key={index} className={styles.listItem}>
                  <div className={styles.itemHeader}>
                    <span className={`${styles.itemTitle} ${styles.itemTitleDanger}`}>
                      {alert.type === "waiting_too_long" ? "Long Wait" : "Call Timeout"}
                    </span>
                  </div>
                  <p className={styles.itemDescription}>{alert.message}</p>
                </li>
              ))}
            </ul>
          </section>
        )}

        <section className={styles.metricsGrid}>
          <article className={styles.metricCard}>
            <span className={styles.metricLabel}>Today&apos;s Appointments</span>
            <strong className={`${styles.metricValue} ${styles.metricValueHighlight}`}>
              {todayAppointments.length}
            </strong>
          </article>
          <article className={styles.metricCard}>
            <span className={styles.metricLabel}>Upcoming Appts.</span>
            <strong className={styles.metricValue}>{upcomingAppointments.length}</strong>
          </article>
          <article className={styles.metricCard}>
            <span className={styles.metricLabel}>Total No-Shows</span>
            <strong className={`${styles.metricValue} ${styles.metricValueDanger}`}>
              {recentNoShows.length}
            </strong>
          </article>
          <article className={styles.metricCard}>
            <span className={styles.metricLabel}>Open Conflicts</span>
            <strong className={styles.metricValue}>{stats.conflicts.length}</strong>
          </article>
        </section>

        <section className={styles.panelsGrid}>
          <article className={styles.panel}>
            <div className={styles.panelHeader}>
              <h2 className={styles.panelTitle}>Recent Audit Log</h2>
            </div>
            {stats.auditLogs.length === 0 ? (
              <div className={styles.emptyState}>
                <p>No recent activity.</p>
              </div>
            ) : (
              <ul className={styles.list}>
                {stats.auditLogs.map((log: any) => (
                  <li key={log.id} className={styles.listItem}>
                    <div className={styles.itemHeader}>
                      <span className={styles.itemTitle}>{log.action}</span>
                      <span className={styles.itemTime}>
                        {new Date(log.created_at).toLocaleString("en-US", {
                          dateStyle: "medium",
                          timeStyle: "short",
                        })}
                      </span>
                    </div>
                    <p className={styles.itemDescription}>
                      By: {log.actor?.display_name || "Unknown"}
                    </p>
                  </li>
                ))}
              </ul>
            )}
          </article>

          <article className={styles.panel}>
            <div className={styles.panelHeader}>
              <h2 className={styles.panelTitle}>Sync Conflicts</h2>
              <Link href="/admin/sync" className={styles.panelLink}>
                View All
              </Link>
            </div>
            {stats.conflicts.length === 0 ? (
              <div className={styles.emptyState}>
                <p>No open conflicts.</p>
              </div>
            ) : (
              <ul className={styles.list}>
                {stats.conflicts.map((c: any) => (
                  <li key={c.id} className={styles.listItem}>
                    <div className={styles.itemHeader}>
                      <span className={`${styles.itemTitle} ${styles.itemTitleDanger}`}>
                        {c.conflict_type}
                      </span>
                      <span className={styles.itemTime}>
                        {new Date(c.created_at).toLocaleString("en-US", {
                          dateStyle: "medium",
                          timeStyle: "short",
                        })}
                      </span>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </article>
        </section>
      </div>
    </AppShell>
  );
}
