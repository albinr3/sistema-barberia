import type { SupabaseClient } from "@supabase/supabase-js";

export async function getActiveServices(supabase: SupabaseClient) {
  const { data, error } = await supabase
    .from("services")
    .select("id, name, description, base_price_cents, duration_minutes, sort_order")
    .eq("is_active", true)
    .order("sort_order", { ascending: true })
    .order("name", { ascending: true });

  if (error) throw new Error("Failed to load services: " + error.message);
  return data;
}

export async function getBookingBarbers(supabase: SupabaseClient) {
  const { data, error } = await supabase.rpc("get_booking_barbers");

  if (error) throw new Error("Failed to load booking barbers: " + error.message);
  return data;
}

export async function getCustomerAppointments(supabase: SupabaseClient) {
  const { data, error } = await supabase
    .from("appointments")
    .select(`
      id, 
      appointment_code,
      starts_at, 
      ends_at, 
      status, 
      cancellation_reason,
      checked_in_at,
      completed_at,
      no_show_at,
      service:services(name),
      barber:barbers(display_name)
    `)
    .order("starts_at", { ascending: false });

  if (error) throw new Error("Failed to load appointments: " + error.message);
  return data;
}

export async function getAdminAppointments(supabase: SupabaseClient) {
  const { data, error } = await supabase
    .from("appointments")
    .select(`
      id, 
      appointment_code,
      starts_at, 
      ends_at, 
      status, 
      cancellation_reason,
      checked_in_at,
      completed_at,
      no_show_at,
      service:services(name),
      barber:barbers(id, display_name),
      customer:profiles(display_name, phone)
    `)
    .order("starts_at", { ascending: false });

  if (error) throw new Error("Failed to load admin appointments: " + error.message);
  return data;
}

export async function getBarberAppointments(supabase: SupabaseClient) {
  const { data, error } = await supabase
    .from("appointments")
    .select(`
      id, 
      appointment_code,
      starts_at, 
      ends_at, 
      status, 
      cancellation_reason,
      checked_in_at,
      completed_at,
      no_show_at,
      service:services(name),
      customer:profiles(display_name, phone)
    `)
    .order("starts_at", { ascending: true })
    .gte("starts_at", new Date().toISOString().split('T')[0] + "T00:00:00Z"); // Today and future

  if (error) throw new Error("Failed to load barber appointments: " + error.message);
  return data;
}

export async function getAdminDashboardStats(supabase: SupabaseClient) {
  const [appointmentsRes, conflictsRes] = await Promise.all([
    supabase
      .from("appointments")
      .select("status, starts_at"),
    supabase
      .from("sync_conflicts")
      .select("id, conflict_type, status, created_at")
      .eq("status", "open")
      .order("created_at", { ascending: false })
  ]);

  return {
    appointments: appointmentsRes.data || [],
    conflicts: conflictsRes.data || []
  };
}

export async function getAdminDailySalesStats(supabase: SupabaseClient) {
  const now = new Date();
  const options = { timeZone: "America/New_York", year: "numeric", month: "2-digit", day: "2-digit" } as const;
  const parts = new Intl.DateTimeFormat("en-US", options).formatToParts(now);
  const year = parts.find((p) => p.type === "year")?.value;
  const month = parts.find((p) => p.type === "month")?.value;
  const day = parts.find((p) => p.type === "day")?.value;
  const todayStr = `${year}-${month}-${day}`;

  // Local bounds for America/New_York (using -04:00 for EDT)
  const startOfDayIso = new Date(`${todayStr}T00:00:00.000-04:00`).toISOString();
  const endOfDayIso = new Date(`${todayStr}T23:59:59.999-04:00`).toISOString();

  const [ticketsRes, paymentsRes] = await Promise.all([
    supabase.from("synced_tickets").select("id, status").eq("ticket_date", todayStr),
    supabase.from("synced_payments").select("amount_cents").gte("collected_at", startOfDayIso).lte("collected_at", endOfDayIso)
  ]);

  const tickets = ticketsRes.data || [];
  const payments = paymentsRes.data || [];

  const createdCount = tickets.length;
  const completedCount = tickets.filter(t => t.status === "completed").length;
  
  // The user requested: created - completed
  // However, cancelled tickets shouldn't count as pending. So we subtract them too.
  const cancelledCount = tickets.filter(t => t.status === "cancelled").length;
  const pendingCount = createdCount - completedCount - cancelledCount;

  const totalSalesCents = payments.reduce((sum, p) => sum + p.amount_cents, 0);

  return {
    totalSalesCents,
    completedServicesCount: completedCount, // The user said "servicios completados" but meant tickets
    pendingCount
  };
}
