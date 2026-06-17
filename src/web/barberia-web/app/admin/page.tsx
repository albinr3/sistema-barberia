/* eslint-disable @typescript-eslint/no-explicit-any */
import { AppShell } from "@/components/layout/app-shell";
import { requireAdmin } from "@/lib/auth/profile";
import { getAdminDashboardStats, getAdminDailySalesStats } from "@/lib/booking/queries";
import { getTicketsDashboardSnapshot } from "@/lib/tickets-dashboard";
import { createClient } from "@/lib/supabase/server";

import styles from "./dashboard.module.css";

export default async function AdminPage() {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const [stats, snapshot, salesStats] = await Promise.all([
    getAdminDashboardStats(supabase),
    getTicketsDashboardSnapshot(supabase),
    getAdminDailySalesStats(supabase)
  ]);

  const todayStr = new Date().toISOString().split("T")[0];

  const todayAppointments = stats.appointments.filter((a) => a.starts_at.startsWith(todayStr));
  const upcomingAppointments = stats.appointments.filter((a) => a.starts_at > todayStr && !a.starts_at.startsWith(todayStr) && a.status !== "cancelled" && a.status !== "no_show");
  const recentNoShows = stats.appointments.filter((a) => a.status === "no_show");

  const formattedSales = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format(salesStats.totalSalesCents / 100);

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

        <section className={styles.panel} style={{ marginBottom: "32px", background: "linear-gradient(to right, var(--surface), var(--surface-low))" }}>
          <div className={styles.panelHeader}>
            <h2 className={styles.panelTitle}>Resumen del Día</h2>
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "24px" }}>
            <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
              <span className={styles.metricLabel}>Ventas de Hoy</span>
              <strong className={`${styles.metricValue} ${styles.metricValueHighlight}`}>
                {formattedSales}
              </strong>
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
              <span className={styles.metricLabel}>Servicios Completados</span>
              <strong className={styles.metricValue}>{salesStats.completedServicesCount}</strong>
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
              <span className={styles.metricLabel}>Tickets Pendientes</span>
              <strong className={styles.metricValue}>{salesStats.pendingCount}</strong>
            </div>
          </div>
        </section>

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

      </div>
    </AppShell>
  );
}
