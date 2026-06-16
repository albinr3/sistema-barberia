import type { SupabaseClient } from "@supabase/supabase-js";

export type TicketHistoryItem = {
  id: string;
  price_cents: number;
  service: {
    id: string;
    name: string;
  } | null;
};

export type TicketHistoryPayment = {
  id: string;
  payment_method: string;
  amount_cents: number;
  receipt_number: string | null;
  payment_reference: string | null;
  collected_at: string;
};

export type TicketHistoryRow = {
  id: string;
  local_ticket_id: string;
  display_ticket_number: number | null;
  customer_name: string | null;
  status: string;
  created_at: string;
  started_at: string | null;
  completed_at: string | null;
  cancelled_at: string | null;
  appointment_id: string | null;
  barber: {
    id: string;
    display_name: string | null;
    station_code: string | null;
  } | null;
  payment: TicketHistoryPayment | null;
  items: TicketHistoryItem[];
};

export type TicketHistoryFilterParams = {
  search?: string;
  startDate?: string;
  endDate?: string;
  barberId?: string;
  status?: string;
  page?: number;
};

export type TicketHistoryResult = {
  tickets: TicketHistoryRow[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
  barbers: { id: string; display_name: string | null }[];
};

const PAGE_SIZE = 20;

export async function getTicketHistory(
  supabase: SupabaseClient,
  params: TicketHistoryFilterParams
): Promise<TicketHistoryResult> {
  const page = params.page || 1;
  const offset = (page - 1) * PAGE_SIZE;

  let query = supabase
    .from("synced_tickets")
    .select(
      `
        id, local_ticket_id, display_ticket_number, customer_name, status, created_at, started_at, completed_at, cancelled_at, appointment_id,
        barber:barbers(id, display_name, station_code),
        payment:synced_payments(id, payment_method, amount_cents, receipt_number, payment_reference, collected_at),
        items:synced_ticket_items(id, price_cents, service:services(id, name))
      `,
      { count: "exact" }
    );

  if (params.search) {
    const searchNumber = parseInt(params.search, 10);
    if (!isNaN(searchNumber)) {
      query = query.or(`customer_name.ilike.%${params.search}%,local_ticket_id.ilike.%${params.search}%,display_ticket_number.eq.${searchNumber}`);
    } else {
      query = query.or(`customer_name.ilike.%${params.search}%,local_ticket_id.ilike.%${params.search}%`);
    }
  }

  if (params.startDate) {
    query = query.gte("created_at", `${params.startDate}T00:00:00Z`);
  }
  
  if (params.endDate) {
    query = query.lte("created_at", `${params.endDate}T23:59:59Z`);
  }

  if (params.barberId) {
    query = query.eq("barber_id", params.barberId);
  }

  if (params.status) {
    query = query.eq("status", params.status);
  }

  query = query
    .order("created_at", { ascending: false })
    .range(offset, offset + PAGE_SIZE - 1);

  const [ticketsResponse, barbersResponse] = await Promise.all([
    query,
    supabase.from("barbers").select("id, display_name").eq("is_active", true).order("display_name")
  ]);

  if (ticketsResponse.error) throw new Error("Failed to load ticket history: " + ticketsResponse.error.message);
  if (barbersResponse.error) throw new Error("Failed to load barbers: " + barbersResponse.error.message);

  const rawTickets = ticketsResponse.data || [];
  const total = ticketsResponse.count || 0;

  const tickets = rawTickets.map((row: unknown) => {
    // Supabase can return arrays or objects for joined relations depending on if it detects uniquely 1-1 or 1-many.
    // payment should ideally be 1 object or an array of 1.
    const typedRow = row as Record<string, unknown>;
    const paymentArray = Array.isArray(typedRow.payment) ? typedRow.payment : (typedRow.payment ? [typedRow.payment] : []);
    const itemsArray = Array.isArray(typedRow.items) ? typedRow.items : (typedRow.items ? [typedRow.items] : []);
    const barberObj = Array.isArray(typedRow.barber) ? typedRow.barber[0] : typedRow.barber;

    return {
      ...typedRow,
      barber: barberObj || null,
      payment: paymentArray.length > 0 ? paymentArray[0] : null,
      items: itemsArray.map((item: unknown) => {
        const typedItem = item as Record<string, unknown>;
        return {
          ...typedItem,
          service: Array.isArray(typedItem.service) ? typedItem.service[0] : typedItem.service
        };
      })
    } as TicketHistoryRow;
  });

  return {
    tickets,
    total,
    page,
    pageSize: PAGE_SIZE,
    totalPages: Math.ceil(total / PAGE_SIZE),
    barbers: barbersResponse.data || []
  };
}

export function formatCurrency(cents: number | undefined | null) {
  if (cents === undefined || cents === null) return "-";
  return new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format(cents / 100);
}

export function formatDateTime(isoString: string | undefined | null) {
  if (!isoString) return "-";
  return new Intl.DateTimeFormat("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(isoString));
}

export function formatStatus(status: string) {
  return status
    .split("_")
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join(" ");
}

export function getSource(ticket: TicketHistoryRow) {
  return ticket.appointment_id ? "Appointment" : "Walk-in";
}
