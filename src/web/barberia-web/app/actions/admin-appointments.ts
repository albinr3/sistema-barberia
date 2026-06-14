"use server";

import { revalidatePath } from "next/cache";
import { createClient } from "@/lib/supabase/server";
import { requireAdmin } from "@/lib/auth/profile";

export async function adminCancelAppointment(appointmentId: string, reason?: string) {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const { data, error } = await supabase.rpc("admin_cancel_appointment", {
    p_appointment_id: appointmentId,
    p_reason: reason || "Cancelled by administrator",
  });

  if (error) {
    console.error("admin_cancel_appointment error", error);
    return { error: error.message || "Failed to cancel appointment" };
  }

  revalidatePath("/admin/appointments");
  return { success: true, appointment: data };
}

export async function adminReassignAppointment(appointmentId: string, newBarberId: string) {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const { data, error } = await supabase.rpc("admin_reassign_appointment", {
    p_appointment_id: appointmentId,
    p_new_barber_id: newBarberId,
  });

  if (error) {
    console.error("admin_reassign_appointment error", error);
    return { error: error.message || "Failed to reassign appointment" };
  }

  revalidatePath("/admin/appointments");
  return { success: true, appointment: data };
}

export async function adminMarkNoShow(appointmentId: string) {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const { data, error } = await supabase.rpc("admin_mark_no_show", {
    p_appointment_id: appointmentId,
  });

  if (error) {
    console.error("admin_mark_no_show error", error);
    return { error: error.message || "Failed to mark no-show" };
  }

  revalidatePath("/admin/appointments");
  return { success: true, appointment: data };
}

export async function adminCompleteAppointment(appointmentId: string) {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const { data, error } = await supabase.rpc("admin_complete_appointment", {
    p_appointment_id: appointmentId,
  });

  if (error) {
    console.error("admin_complete_appointment error", error);
    return { error: error.message || "Failed to complete appointment" };
  }

  revalidatePath("/admin/appointments");
  return { success: true, appointment: data };
}

export async function adminGetRescheduleSlots(appointmentId: string, date: string) {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const { data, error } = await supabase.rpc("admin_get_reschedule_slots", {
    p_appointment_id: appointmentId,
    p_date: date,
  });

  if (error) {
    console.error("admin_get_reschedule_slots error", error);
    return { error: error.message || "Failed to load reschedule slots" };
  }

  return { success: true, slots: data || [] };
}

export async function adminRescheduleAppointment(appointmentId: string, newStartsAt: string) {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const { data, error } = await supabase.rpc("admin_reschedule_appointment", {
    p_appointment_id: appointmentId,
    p_new_starts_at: newStartsAt,
  });

  if (error) {
    console.error("admin_reschedule_appointment error", error);
    return { error: error.message || "Failed to reschedule appointment" };
  }

  revalidatePath("/admin/appointments");
  revalidatePath("/app/appointments");
  revalidatePath("/barber");
  return { success: true, appointment: data };
}
