import { AppShell } from "@/components/layout/app-shell";
import { Button } from "@/components/ui/button";
import { requireAdmin } from "@/lib/auth/profile";
import { activeOnly } from "@/lib/catalog/filters";
import { getAdminCatalogData } from "@/lib/catalog/queries";
import { createClient } from "@/lib/supabase/server";
import { BarberManager } from "./barber-manager";
import styles from "./catalog.module.css";
import { ExceptionManager } from "./exception-manager";
import { ServiceManager } from "./service-manager";
import { WeeklyAvailabilityEditor } from "./weekly-availability-editor";
import Link from "next/link";
import { ChevronLeft, ChevronRight } from "lucide-react";

type CatalogPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

type AutoReassignmentEventRow = {
  id: string;
  received_at: string;
  processed_at: string | null;
  status: string | null;
  source_device_id: string | null;
  payload: unknown;
};

type AutoReassignmentRecord = {
  id: string;
  occurredAt: string;
  displayTicketNumber: number | null;
  internalTicketNumber: string | null;
  previousBarberName: string | null;
  previousStationCode: string | null;
  targetBarberName: string | null;
  targetStationCode: string | null;
  outcome: string | null;
  resultTurnState: string | null;
  previousBarberReleased: boolean;
  status: string | null;
  sourceDeviceId: string | null;
};

function firstParam(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function isoDate(offsetDays = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offsetDays);
  return date.toISOString().slice(0, 10);
}

function Section({
  title,
  description,
  children,
  secondary,
  collapsible,
}: {
  title: string;
  description: string;
  children: React.ReactNode;
  secondary?: boolean;
  collapsible?: boolean;
}) {
  if (collapsible) {
    return (
      <details className={`${styles.section} ${styles.collapsibleSection} ${secondary ? styles.sectionSecondary : ""}`}>
        <summary className={styles.sectionHeader}>
          <div>
            <h2>{title}</h2>
            <p>{description}</p>
          </div>
        </summary>
        <div className={styles.sectionContent}>
          {children}
        </div>
      </details>
    );
  }

  return (
    <section className={`${styles.section} ${secondary ? styles.sectionSecondary : ""}`}>
      <div className={styles.sectionHeader}>
        <div>
          <h2>{title}</h2>
          <p>{description}</p>
        </div>
      </div>
      {children}
    </section>
  );
}

function StatusMessage({ success, error }: { success?: string; error?: string }) {
  if (!success && !error) {
    return null;
  }

  return (
    <div className={error ? styles.error : styles.notice} role="status">
      {error ?? success}
    </div>
  );
}

function EmptyState({ children }: { children: React.ReactNode }) {
  return <p className={styles.empty}>{children}</p>;
}

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" && !Array.isArray(value) ? value as Record<string, unknown> : {};
}

function stringValue(value: unknown): string | null {
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : null;
}

function numberValue(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function boolValue(value: unknown): boolean {
  return value === true;
}

function formatAdminDateTime(value: string) {
  return new Intl.DateTimeFormat("en-US", {
    timeZone: "America/New_York",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(value));
}

function formatOutcome(outcome: string | null, resultTurnState: string | null) {
  if (outcome === "started" || resultTurnState === "InService") {
    return "Started service";
  }

  if (outcome === "waiting" || resultTurnState === "Waiting") {
    return "Moved to waiting";
  }

  return "Transferred";
}

function parseAutoReassignment(row: AutoReassignmentEventRow): AutoReassignmentRecord {
  const payload = asRecord(row.payload);
  return {
    id: row.id,
    occurredAt: stringValue(payload.occurred_at) ?? row.processed_at ?? row.received_at,
    displayTicketNumber: numberValue(payload.display_ticket_number),
    internalTicketNumber: stringValue(payload.internal_ticket_number),
    previousBarberName: stringValue(payload.previous_barber_name),
    previousStationCode: stringValue(payload.previous_station_code),
    targetBarberName: stringValue(payload.target_barber_name),
    targetStationCode: stringValue(payload.target_station_code),
    outcome: stringValue(payload.outcome),
    resultTurnState: stringValue(payload.result_turn_state),
    previousBarberReleased: boolValue(payload.previous_barber_released),
    status: row.status,
    sourceDeviceId: row.source_device_id,
  };
}

export default async function AdminCatalogPage({ searchParams }: CatalogPageProps) {
  const params = await searchParams;
  const page = parseInt(firstParam(params.page) || "1", 10);
  const limit = 5;
  const offset = (page - 1) * limit;

  const supabase = await createClient();
  await requireAdmin(supabase);
  const [data, autoReassignmentsResponse] = await Promise.all([
    getAdminCatalogData(supabase),
    supabase
      .from("sync_events")
      .select("id, received_at, processed_at, status, source_device_id, payload", { count: "exact" })
      .eq("event_type", "ticket.auto_reassigned")
      .order("received_at", { ascending: false })
      .range(offset, offset + limit - 1),
  ]);

  if (autoReassignmentsResponse.error) {
    throw new Error(`Unable to load automatic reassignments: ${autoReassignmentsResponse.error.message}`);
  }

  const totalReassignments = autoReassignmentsResponse.count || 0;
  const totalPages = Math.ceil(totalReassignments / limit);

  const autoReassignments = ((autoReassignmentsResponse.data ?? []) as AutoReassignmentEventRow[])
    .map(parseAutoReassignment);

  const activeServices = activeOnly(data.services);

  const metrics = {
    activeBarbers: activeOnly(data.barbers).length,
    activeServices: activeServices.length,
    activeRules: data.availabilityRules.filter((r) => r.is_active).length,
    upcomingExceptions: data.availabilityExceptions.length,
  };

  return (
    <AppShell title="Admin Dashboard" variant="admin">
      <div className={styles.page}>
        <StatusMessage success={firstParam(params.success)} error={firstParam(params.error)} />

        <div className={styles.summaryGrid}>
          <div className={styles.summaryCard}>
            <span>Active Barbers</span>
            <strong>{metrics.activeBarbers}</strong>
          </div>
          <div className={styles.summaryCard}>
            <span>Active Services</span>
            <strong>{metrics.activeServices}</strong>
          </div>
          <div className={styles.summaryCard}>
            <span>Weekly Rules</span>
            <strong>{metrics.activeRules}</strong>
          </div>
          <div className={styles.summaryCard}>
            <span>Upcoming Exceptions</span>
            <strong>{metrics.upcomingExceptions}</strong>
          </div>
        </div>

        <Section
          title="Barbers"
          description="Manage the web catalog authority for active barbers, stations, image paths and kiosk selection."
          collapsible
        >
          <BarberManager barbers={data.barbers} />
        </Section>

        <Section title="Services" description="Control prices, duration, display order and service visibility." collapsible>
          <ServiceManager services={data.services} />
        </Section>

        <Section title="Weekly availability" description="Define recurring local business hours by barber.">
          <WeeklyAvailabilityEditor barbers={data.barbers} initialRules={data.availabilityRules} />
        </Section>

        <div className={styles.secondaryGrid}>
          <Section secondary title="Date exceptions" description="Close a day or replace normal hours for a specific date.">
            <ExceptionManager barbers={data.barbers} exceptions={data.availabilityExceptions} />
          </Section>
        </div>

        <Section
          secondary
          title="Automatic ticket reassignments"
          description="Recent walk-in tickets moved from one barber station to another when service was started."
        >
          {autoReassignments.length > 0 ? (
            <>
              <div className={styles.reassignmentList}>
                {autoReassignments.map((event) => (
                  <article key={event.id} className={styles.reassignmentRow}>
                    <div className={styles.reassignmentMain}>
                      <div>
                        <span className={styles.reassignmentTicket}>
                          Ticket {event.displayTicketNumber ?? event.internalTicketNumber ?? "Unknown"}
                        </span>
                        <strong>
                          De: {event.previousBarberName ? `${event.previousBarberName} (${event.previousStationCode ?? '?'})` : (event.previousStationCode ?? "Espera General")}
                          {" -> "}
                          A: {event.targetBarberName ? `${event.targetBarberName} (${event.targetStationCode ?? '?'})` : (event.targetStationCode ?? "Nuevo")}
                        </strong>
                      </div>
                      <span className={styles.reassignmentOutcome}>
                        {formatOutcome(event.outcome, event.resultTurnState)}
                      </span>
                    </div>
                    <div className={styles.reassignmentMeta}>
                      <span>{formatAdminDateTime(event.occurredAt)}</span>
                      <span>{event.previousBarberReleased ? "Previous barber released" : "No previous barber release"}</span>
                      <span>Sync {event.status ?? "unknown"}</span>
                    </div>
                  </article>
                ))}
              </div>
              {totalPages > 1 && (
                <div className={styles.pagination}>
                  <Link href={`/admin/admin-dashboard?page=${page - 1}`} className={styles.pageBtn} aria-disabled={page <= 1} tabIndex={page <= 1 ? -1 : 0} style={{ pointerEvents: page <= 1 ? 'none' : 'auto', opacity: page <= 1 ? 0.5 : 1 }}>
                    <ChevronLeft size={16} /> Previous
                  </Link>
                  <span className={styles.pageText}>Page {page} of {totalPages}</span>
                  <Link href={`/admin/admin-dashboard?page=${page + 1}`} className={styles.pageBtn} aria-disabled={page >= totalPages} tabIndex={page >= totalPages ? -1 : 0} style={{ pointerEvents: page >= totalPages ? 'none' : 'auto', opacity: page >= totalPages ? 0.5 : 1 }}>
                    Next <ChevronRight size={16} />
                  </Link>
                </div>
              )}
            </>
          ) : (
            <EmptyState>No automatic ticket reassignments recorded yet.</EmptyState>
          )}
        </Section>
      </div>
    </AppShell>
  );
}
