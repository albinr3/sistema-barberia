"use server";

import { revalidatePath } from "next/cache";
import { createClient } from "@/lib/supabase/server";

export async function createAppointment(serviceId: string, barberId: string, startsAt: string) {
  const supabase = await createClient();

  // ensure session exists
  const { data: { session } } = await supabase.auth.getSession();
  if (!session) {
    return { error: "Not authenticated" };
  }

  // Call RPC
  const { data, error } = await supabase.rpc("create_appointment", {
    p_service_id: serviceId,
    p_barber_id: barberId,
    p_starts_at: startsAt,
  });

  if (error) {
    console.error("create_appointment error", error);
    return { error: error.message || "Failed to create appointment" };
  }

  revalidatePath("/app/appointments");
  revalidatePath("/app/book");
  
  return { success: true, appointment: data };
}

export async function cancelCustomerAppointment(appointmentId: string, reason?: string) {
  const supabase = await createClient();

  const { data: { session } } = await supabase.auth.getSession();
  if (!session) {
    return { error: "Not authenticated" };
  }

  const { data, error } = await supabase.rpc("cancel_customer_appointment", {
    p_appointment_id: appointmentId,
    p_reason: reason || "Cancelled by customer",
  });

  if (error) {
    console.error("cancel_customer_appointment error", error);
    return { error: error.message || "Failed to cancel appointment" };
  }

  revalidatePath("/app/appointments");
  return { success: true, appointment: data };
}

export async function getAvailableSlotsAction(serviceId: string, date: string, barberId?: string) {
  const supabase = await createClient();

  const { data: { session } } = await supabase.auth.getSession();
  if (!session) {
    return { error: "Not authenticated" };
  }

  const { data, error } = await supabase.rpc("get_available_slots", {
    service_id: serviceId,
    starts_on: date,
    ends_on: date,
    barber_id: barberId || null,
  });

  if (error) {
    console.error("get_available_slots error", error);
    return { error: error.message || "Failed to fetch slots" };
  }

  return { success: true, slots: data };
}
