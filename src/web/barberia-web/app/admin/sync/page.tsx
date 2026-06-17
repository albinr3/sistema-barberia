import { AppShell } from "@/components/layout/app-shell";
import { requireAdmin } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";
import { Activity, Server, Smartphone, AlertTriangle, ChevronLeft, ChevronRight, FileText } from "lucide-react";
import styles from "./admin-sync.module.css";
import { DismissConflictButton } from "@/components/admin/dismiss-conflict-button";
import Link from "next/link";

export default async function AdminSyncPage(props: { searchParams?: Promise<{ page?: string }> }) {
  const searchParams = await props.searchParams;
  const page = parseInt(searchParams?.page || "1", 10);
  const limit = 15;
  const offset = (page - 1) * limit;

  const supabase = await createClient();
  await requireAdmin(supabase);

  const [
    { data: devices },
    { count: barbersCount },
    { count: servicesCount },
    { data: events, count: totalEvents },
    { data: conflicts },
    { data: auditLogs },
  ] = await Promise.all([
    supabase.from("sync_devices").select("*").order("last_sync_at", { ascending: false }),
    supabase.from("barbers").select("*", { count: "exact", head: true }),
    supabase.from("services").select("*", { count: "exact", head: true }),
    supabase.from("sync_events").select("*", { count: "exact" }).order("received_at", { ascending: false }).range(offset, offset + limit - 1),
    supabase.from("sync_conflicts").select("*").eq("status", "open").order("created_at", { ascending: false }),
    supabase.from("audit_log").select("id, action, created_at, actor:profiles(display_name)").order("created_at", { ascending: false }).limit(10),
  ]);

  const totalPages = Math.ceil((totalEvents || 0) / limit);

  return (
    <AppShell title="Sync Dashboard" variant="admin">
      <div className={styles.container}>
        <div className={styles.header}>
          <h1 className={styles.pageTitle}>Sync Dashboard</h1>
          <p className={styles.pageSubtitle}>Monitor synchronization between the cloud and desktop devices.</p>
        </div>

        <section className={styles.section}>
          <h2 className={styles.sectionTitle}><Activity size={20} /> System Overview</h2>
          <div className={styles.statsGrid}>
            <div className={styles.statCard}>
              <p className={styles.statLabel}>Synced Barbers</p>
              <p className={styles.statValue}>{barbersCount || 0}</p>
            </div>
            <div className={styles.statCard}>
              <p className={styles.statLabel}>Synced Services</p>
              <p className={styles.statValue}>{servicesCount || 0}</p>
            </div>
          </div>
        </section>

        <section className={styles.section}>
          <h2 className={styles.sectionTitle}><Server size={20} /> Desktop Devices</h2>
          {devices && devices.length > 0 ? (
            <div className={styles.devicesGrid}>
              {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
              {devices.map((device: any) => {
                const isOnline = device.last_sync_at && new Date().getTime() - new Date(device.last_sync_at).getTime() < 1000 * 60 * 15;
                return (
                  <div key={device.id} className={styles.deviceCard}>
                    <div className={styles.deviceInfo}>
                      <p className={styles.deviceName}>
                        <Smartphone size={16} className="inline mr-2 text-[var(--muted)]" />
                        {device.name || "Desktop Client"}
                      </p>
                      <p className={styles.deviceId}>ID: {device.id}</p>
                      {device.last_sync_at && (
                        <p className={styles.deviceTime}>Last seen: {new Date(device.last_sync_at).toLocaleString()}</p>
                      )}
                    </div>
                    <div className={`${styles.statusIndicator} ${isOnline ? styles.online : styles.offline}`}>
                      <div className={styles.statusDot}></div>
                      {isOnline ? "Online" : "Offline"}
                    </div>
                  </div>
                );
              })}
            </div>
          ) : (
            <div className={styles.emptyMessage}>No desktop devices registered yet.</div>
          )}
        </section>

        <section className={styles.section}>
          <h2 className={styles.sectionTitle}><AlertTriangle size={20} /> Open Conflicts</h2>
          {conflicts && conflicts.length > 0 ? (
            <div className={styles.conflictsGrid}>
              {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
              {conflicts.map((conflict: any) => (
                <div key={conflict.id} className={styles.conflictCard}>
                  <div className={styles.conflictHeader}>
                    <div>
                      <p className={styles.conflictType}>{conflict.conflict_type.replace(/_/g, ' ').toUpperCase()}</p>
                      <p className={styles.conflictAggregate}>Target: {conflict.aggregate_type} ({conflict.aggregate_id})</p>
                      <p className={styles.conflictTime}>Detected: {new Date(conflict.created_at).toLocaleString()}</p>
                    </div>
                    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '0.5rem' }}>
                      <span className={styles.statusPending} style={{ color: 'var(--danger)', backgroundColor: 'rgba(239, 68, 68, 0.1)' }}>Open Issue</span>
                      <DismissConflictButton conflictId={conflict.id} />
                    </div>
                  </div>
                  <pre className={styles.conflictPayload}>
                    {JSON.stringify(conflict.local_payload, null, 2)}
                  </pre>
                </div>
              ))}
            </div>
          ) : (
            <div className={styles.emptyMessage}>All systems are synchronized. No open conflicts.</div>
          )}
        </section>

        <section className={styles.section}>
          <h2 className={styles.sectionTitle}>Recent Sync Events</h2>
          {events && events.length > 0 ? (
            <>
              <div className={styles.tableContainer}>
                <table className={styles.table}>
                <thead>
                  <tr>
                    <th>Time</th>
                    <th>Source</th>
                    <th>Event Type</th>
                    <th>Status</th>
                    <th>Device ID</th>
                  </tr>
                </thead>
                <tbody>
                  {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
                  {events?.map((event: any) => (
                    <tr key={event.id}>
                      <td>{new Date(event.received_at).toLocaleString()}</td>
                      <td className={styles.monoText}>{event.source}</td>
                      <td style={{ fontWeight: 500 }}>{event.event_type}</td>
                      <td>
                        <span className={event.status === "processed" ? styles.statusCompleted : styles.statusPending}>
                          {event.status === "processed" ? "Processed" : "Pending"}
                        </span>
                      </td>
                      <td className={styles.monoText}>{event.source_device_id || "Unknown"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {totalPages > 1 && (
              <div className={styles.pagination}>
                <Link href={`/admin/sync?page=${page - 1}`} className={styles.pageBtn} aria-disabled={page <= 1} tabIndex={page <= 1 ? -1 : 0} style={{ pointerEvents: page <= 1 ? 'none' : 'auto', opacity: page <= 1 ? 0.5 : 1 }}>
                  <ChevronLeft size={16} /> Previous
                </Link>
                <span className={styles.pageText}>Page {page} of {totalPages}</span>
                <Link href={`/admin/sync?page=${page + 1}`} className={styles.pageBtn} aria-disabled={page >= totalPages} tabIndex={page >= totalPages ? -1 : 0} style={{ pointerEvents: page >= totalPages ? 'none' : 'auto', opacity: page >= totalPages ? 0.5 : 1 }}>
                  Next <ChevronRight size={16} />
                </Link>
              </div>
            )}
            </>
          ) : (
            <div className={styles.emptyMessage}>No recent sync events found.</div>
          )}
        </section>

        <section className={styles.section}>
          <h2 className={styles.sectionTitle}><FileText size={20} /> Recent Audit Log</h2>
          {auditLogs && auditLogs.length > 0 ? (
            <div className={styles.tableContainer}>
              <table className={styles.table}>
                <thead>
                  <tr>
                    <th>Time</th>
                    <th>Action</th>
                    <th>Actor</th>
                  </tr>
                </thead>
                <tbody>
                  {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
                  {auditLogs.map((log: any) => (
                    <tr key={log.id}>
                      <td>{new Date(log.created_at).toLocaleString()}</td>
                      <td style={{ fontWeight: 500 }}>{log.action}</td>
                      <td>{log.actor?.display_name || "Unknown"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className={styles.emptyMessage}>No recent audit activity.</div>
          )}
        </section>
      </div>
    </AppShell>
  );
}
