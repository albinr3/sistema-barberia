import type { SupabaseClient } from "@supabase/supabase-js";

export type TicketDashboardTicketRow = {
  id: string;
  local_ticket_id: string;
  display_ticket_number: number | null;
  ticket_date: string | null;
  customer_name: string | null;
  barber_id: string | null;
  status: string;
  created_at: string;
  checked_in_at: string | null;
  updated_at: string;
  barber: {
    id: string;
    display_name: string | null;
    station_code: string | null;
  } | null;
};

export type TicketDashboardBarberRow = {
  id: string;
  display_name: string | null;
  station_code: string | null;
  is_active: boolean;
  is_available_locally?: boolean | null;
};

export type TicketDashboardDeviceRow = {
  id: string;
  name: string | null;
  last_sync_at: string | null;
};

export type TicketAlert = {
  ticketId: string;
  type: "waiting_too_long" | "called_too_long";
  message: string;
};

export type TicketDashboardSnapshot = {
  loadedAt: Date;
  nowCalling: TicketDashboardTicketRow[];
  waiting: TicketDashboardTicketRow[];
  activeQueue: TicketDashboardTicketRow[];
  alerts: TicketAlert[];
  barbers: TicketDashboardBarber[];
  waitingTotal: number;
  lastSyncAt: string | null;
  isStale: boolean;
};

export type TicketDashboardBarber = TicketDashboardBarberRow & {
  status: "available" | "calling" | "busy" | "offline";
  detail: string;
  activeTicket: TicketDashboardTicketRow | null;
};

const ACTIVE_TICKET_STATUSES = ["waiting", "called", "in_progress"];
const STALE_SYNC_MINUTES = 15;

export async function getTicketsDashboardSnapshot(supabase: SupabaseClient): Promise<TicketDashboardSnapshot> {
  const [{ data: tickets, error: ticketsError }, { data: barbers, error: barbersError }, { data: devices }] =
    await Promise.all([
      supabase
        .from("synced_tickets")
        .select(`
          id,
          local_ticket_id,
          display_ticket_number,
          ticket_date,
          customer_name,
          barber_id,
          status,
          created_at,
          checked_in_at,
          updated_at,
          barber:barbers(id, display_name, station_code)
        `)
        .is("appointment_id", null)
        .is("restore_reverted_at", null)
        .in("status", ACTIVE_TICKET_STATUSES)
        .order("checked_in_at", { ascending: true, nullsFirst: false })
        .order("created_at", { ascending: true }),
      supabase
        .from("barbers")
        .select("id, display_name, station_code, is_active, is_available_locally")
        .eq("is_active", true),
      supabase.from("sync_devices").select("id, name, last_sync_at").order("last_sync_at", { ascending: false }),
    ]);

  if (ticketsError) throw new Error("Failed to load synced tickets: " + ticketsError.message);
  if (barbersError) throw new Error("Failed to load synced barbers: " + barbersError.message);

  return buildTicketsDashboardSnapshot({
    tickets: normalizeTicketRows(tickets ?? []),
    barbers: (barbers ?? []) as TicketDashboardBarberRow[],
    devices: (devices ?? []) as TicketDashboardDeviceRow[],
    loadedAt: new Date(),
  });
}

function normalizeTicketRows(rows: unknown): TicketDashboardTicketRow[] {
  return (rows as Array<Omit<TicketDashboardTicketRow, "barber"> & {
    barber: TicketDashboardTicketRow["barber"] | TicketDashboardTicketRow["barber"][];
  }>).map((row) => ({
    ...row,
    barber: Array.isArray(row.barber) ? row.barber[0] ?? null : row.barber,
  }));
}

export function buildTicketsDashboardSnapshot(input: {
  tickets: TicketDashboardTicketRow[];
  barbers: TicketDashboardBarberRow[];
  devices?: TicketDashboardDeviceRow[];
  loadedAt: Date;
}): TicketDashboardSnapshot {
  const todayStr = getLocalTodayString(input.loadedAt);

  const todayTickets = input.tickets.filter((ticket) => {
    if (ticket.ticket_date) {
      return ticket.ticket_date === todayStr;
    }
    if (ticket.created_at) {
      return getLocalTodayString(new Date(ticket.created_at)) === todayStr;
    }
    return false;
  });

  const orderedTickets = [...todayTickets].sort(compareTicketsByArrival);
  const nowCalling = orderedTickets.filter((ticket) => ticket.status === "called").slice(0, 4);
  const waiting = orderedTickets.filter((ticket) => ticket.status === "waiting");
  const activeQueue = orderedTickets.filter((ticket) => ["waiting", "called", "in_progress"].includes(ticket.status));
  const activeTicketsByBarber = getActiveTicketsByBarber(orderedTickets);
  const lastSyncAt = getLastSyncAt(input.devices ?? []);

  const alerts: TicketAlert[] = [];
  for (const ticket of orderedTickets) {
    if (ticket.status === "waiting") {
      const waitTimeMinutes = (input.loadedAt.getTime() - Date.parse(ticket.checked_in_at ?? ticket.created_at)) / 60000;
      if (waitTimeMinutes >= 30) {
        alerts.push({
          ticketId: ticket.id,
          type: "waiting_too_long",
          message: `Ticket ${formatTicketNumber(ticket)} has been waiting for ${Math.floor(waitTimeMinutes)} minutes.`,
        });
      }
    } else if (ticket.status === "called") {
      const calledTimeMinutes = (input.loadedAt.getTime() - Date.parse(ticket.updated_at)) / 60000;
      if (calledTimeMinutes >= 4) {
        alerts.push({
          ticketId: ticket.id,
          type: "called_too_long",
          message: `Ticket ${formatTicketNumber(ticket)} has been called for ${Math.floor(calledTimeMinutes)} minutes.`,
        });
      }
    }
  }

  return {
    loadedAt: input.loadedAt,
    nowCalling,
    waiting,
    activeQueue,
    alerts,
    waitingTotal: waiting.length,
    barbers: [...input.barbers].sort(compareBarbers).map((barber) => {
      const activeTicket = activeTicketsByBarber.get(barber.id) ?? null;
      return {
        ...barber,
        activeTicket,
        ...getBarberDisplay(barber, activeTicket),
      };
    }),
    lastSyncAt,
    isStale: isSyncStale(lastSyncAt, input.loadedAt),
  };
}

export function formatTicketNumber(ticket: TicketDashboardTicketRow) {
  return ticket.display_ticket_number?.toString() ?? ticket.local_ticket_id;
}

function getActiveTicketsByBarber(tickets: TicketDashboardTicketRow[]) {
  const map = new Map<string, TicketDashboardTicketRow>();
  for (const ticket of tickets) {
    if (!ticket.barber_id) continue;
    const current = map.get(ticket.barber_id);
    if (!current || ticketPriority(ticket) < ticketPriority(current)) {
      map.set(ticket.barber_id, ticket);
    }
  }
  return map;
}

function getBarberDisplay(barber: TicketDashboardBarberRow, activeTicket: TicketDashboardTicketRow | null) {
  if (activeTicket?.status === "called") {
    return {
      status: "calling" as const,
      detail: `Calling: ${formatTicketNumber(activeTicket)}`,
    };
  }

  if (activeTicket?.status === "in_progress") {
    return {
      status: "busy" as const,
      detail: `Serving: ${formatTicketNumber(activeTicket)}`,
    };
  }

  if (barber.is_available_locally === false) {
    return {
      status: "offline" as const,
      detail: barber.station_code ? `${barber.station_code} Not available` : "Not available",
    };
  }

  return {
    status: "available" as const,
    detail: `Station ${barber.station_code ?? "B-?"} Ready`,
  };
}

function getLastSyncAt(devices: TicketDashboardDeviceRow[]) {
  return devices
    .map((device) => device.last_sync_at)
    .filter((value): value is string => Boolean(value))
    .sort((left, right) => Date.parse(right) - Date.parse(left))[0] ?? null;
}

function isSyncStale(lastSyncAt: string | null, loadedAt: Date) {
  if (!lastSyncAt) return true;
  return loadedAt.getTime() - Date.parse(lastSyncAt) > STALE_SYNC_MINUTES * 60 * 1000;
}

function compareTicketsByArrival(left: TicketDashboardTicketRow, right: TicketDashboardTicketRow) {
  return Date.parse(left.checked_in_at ?? left.created_at) - Date.parse(right.checked_in_at ?? right.created_at);
}

function compareBarbers(left: TicketDashboardBarberRow, right: TicketDashboardBarberRow) {
  const leftStation = stationNumber(left.station_code);
  const rightStation = stationNumber(right.station_code);
  if (leftStation !== rightStation) return leftStation - rightStation;
  return (left.display_name ?? "").localeCompare(right.display_name ?? "");
}

function stationNumber(stationCode: string | null) {
  const match = stationCode?.match(/\d+/);
  return match ? Number(match[0]) : Number.MAX_SAFE_INTEGER;
}

function ticketPriority(ticket: TicketDashboardTicketRow) {
  if (ticket.status === "called") return 0;
  if (ticket.status === "in_progress") return 1;
  return 2;
}

function getLocalTodayString(date: Date) {
  const options = { timeZone: "America/New_York", year: "numeric", month: "2-digit", day: "2-digit" } as const;
  const parts = new Intl.DateTimeFormat("en-US", options).formatToParts(date);
  const year = parts.find((p) => p.type === "year")?.value;
  const month = parts.find((p) => p.type === "month")?.value;
  const day = parts.find((p) => p.type === "day")?.value;
  return `${year}-${month}-${day}`;
}
