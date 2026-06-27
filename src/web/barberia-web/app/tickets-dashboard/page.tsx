import { AlertTriangle, Clock3, Scissors, UsersRound, Menu } from "lucide-react";
import Link from "next/link";
import { AutoRefresh } from "@/components/tickets-dashboard/auto-refresh";
import { requireAdmin } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";
import { formatTicketNumber, getTicketsDashboardSnapshot, type TicketDashboardBarber, type TicketDashboardTicketRow } from "@/lib/tickets-dashboard";
import styles from "./tickets-dashboard.module.css";

export const dynamic = "force-dynamic";

export default async function TicketsDashboardPage() {
  const supabase = await createClient();
  await requireAdmin(supabase);
  const snapshot = await getTicketsDashboardSnapshot(supabase);

  return (
    <main className={styles.screen}>
      <AutoRefresh intervalMs={30_000} />
      <section className={styles.nowCalling} aria-label="Now Calling">
        <div className={styles.sectionHeader}>
          <div className={styles.titleGroup}>
            <Link href="/admin" className={styles.menuButton} aria-label="Open menu">
              <Menu size={28} aria-hidden="true" />
            </Link>
            <Clock3 size={34} aria-hidden="true" />
            <h1>Now Calling</h1>
          </div>
          <div className={styles.headerPills}>
            {snapshot.isStale ? (
              <span className={`${styles.pill} ${styles.warningPill}`}>
                <AlertTriangle size={16} aria-hidden="true" />
                Sync stale
              </span>
            ) : null}
            <span className={styles.pill}>Updated: {formatTime(snapshot.loadedAt)}</span>
          </div>
        </div>

        {snapshot.nowCalling.length > 0 ? (
          <div className={styles.callingGrid}>
            {snapshot.nowCalling.map((ticket) => (
              <CallingCard key={ticket.id} ticket={ticket} />
            ))}
          </div>
        ) : (
          <EmptyState text="No tickets are being called." />
        )}
      </section>

      <div className={styles.lowerGrid}>
        <section className={styles.panel} aria-label="Waiting List">
          <div className={styles.panelHeader}>
            <div className={styles.panelTitle}>
              <UsersRound size={25} aria-hidden="true" />
              <h2>Waiting List</h2>
            </div>
            <strong className={styles.waitingTotal}>Total Wait: {snapshot.waitingTotal}</strong>
          </div>

          {snapshot.waiting.length > 0 ? (
            <div className={styles.waitingList}>
              {snapshot.waiting.slice(0, 12).map((ticket) => (
                <WaitingCard key={ticket.id} ticket={ticket} />
              ))}
            </div>
          ) : (
            <EmptyState text="No customers waiting." />
          )}
        </section>

        <section className={styles.panel} aria-label="Barber Status">
          <div className={styles.panelHeader}>
            <div className={styles.panelTitle}>
              <Scissors size={25} aria-hidden="true" />
              <h2>Barber Status</h2>
            </div>
          </div>

          {snapshot.barbers.length > 0 ? (
            <div className={styles.barberGrid}>
              {snapshot.barbers.map((barber, index) => (
                <BarberCard key={barber.id} barber={barber} order={index + 1} />
              ))}
            </div>
          ) : (
            <EmptyState text="No active barbers registered." />
          )}
        </section>
      </div>
    </main>
  );
}

function CallingCard({ ticket }: { ticket: TicketDashboardTicketRow }) {
  const barberName = ticket.barber?.display_name ?? "Local barber";
  const stationCode = ticket.barber?.station_code ?? "B-?";

  return (
    <article className={styles.callingCard}>
      <div className={styles.callingStripe} />
      <div className={styles.callingTicket}>
        <span>Ticket Number</span>
        <strong>{formatTicketNumber(ticket)}</strong>
      </div>
      <div className={styles.callingDestination}>
        <div>
          <span>Station</span>
          <strong>{stationCode}</strong>
        </div>
        <div className={styles.avatar}>{initials(barberName)}</div>
        <b>{barberName}</b>
      </div>
    </article>
  );
}

function WaitingCard({ ticket }: { ticket: TicketDashboardTicketRow }) {
  return (
    <article className={styles.waitingCard}>
      <strong className={styles.ticketBox}>{formatTicketNumber(ticket)}</strong>
      <div className={styles.waitingDetails}>
        <b>{ticket.customer_name || "Walk-in customer"}</b>
        <span>{ticket.barber ? `Requested: ${ticket.barber.station_code ?? ""} ${ticket.barber.display_name ?? "Local barber"}` : "Requested: Any Available"}</span>
      </div>
      <time>{formatTime(ticket.checked_in_at ?? ticket.created_at)}</time>
    </article>
  );
}

function BarberCard({ barber, order }: { barber: TicketDashboardBarber; order: number }) {
  return (
    <article className={`${styles.barberCard} ${styles[barber.status]}`}>
      <div className={styles.statusStripe} />
      <div className={styles.orderNumber}>{barber.status === "available" ? order : null}</div>
      <div className={styles.avatar}>{initials(barber.display_name)}</div>
      <div className={styles.barberDetails}>
        <div className={styles.barberHeading}>
          <b>{barber.display_name ?? "Local barber"}({barber.station_code ?? "B-?"})</b>
          <span>{statusLabel(barber.status)}</span>
        </div>
        <p>{barber.detail}</p>
      </div>
    </article>
  );
}

function EmptyState({ text }: { text: string }) {
  return <div className={styles.emptyState}>{text}</div>;
}

function formatTime(value: string | Date) {
  const date = typeof value === "string" ? new Date(value) : value;
  return new Intl.DateTimeFormat("en-US", {
    hour: "numeric",
    minute: "2-digit",
  }).format(date);
}

function initials(name: string | null | undefined) {
  const parts = name?.split(" ").filter(Boolean) ?? [];
  return parts.slice(0, 2).map((part) => part[0]?.toUpperCase()).join("") || "?";
}

function statusLabel(status: TicketDashboardBarber["status"]) {
  switch (status) {
    case "calling":
      return "Calling";
    case "busy":
      return "Busy";
    case "offline":
      return "On Break";
    case "available":
    default:
      return "Available";
  }
}
