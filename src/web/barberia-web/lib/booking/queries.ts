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
  const [appointmentsRes, conflictsRes, auditRes] = await Promise.all([
    supabase
      .from("appointments")
      .select("status, starts_at"),
    supabase
      .from("sync_conflicts")
      .select("id, conflict_type, status, created_at")
      .eq("status", "open")
      .order("created_at", { ascending: false }),
    supabase
      .from("audit_log")
      .select("id, action, created_at, actor:profiles(display_name)")
      .order("created_at", { ascending: false })
      .limit(10)
  ]);

  return {
    appointments: appointmentsRes.data || [],
    conflicts: conflictsRes.data || [],
    auditLogs: auditRes.data || []
  };
}
