"use server";

import { revalidatePath } from "next/cache";
import { createClient } from "@/lib/supabase/server";
import { requireAdmin } from "@/lib/auth/profile";

type ActionResult = {
  success?: boolean;
  commandId?: string;
  error?: string;
};

export async function requestPayrollSnapshot(_previousState: ActionResult | null, formData: FormData): Promise<ActionResult> {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const sourceDeviceId = requiredString(formData, "sourceDeviceId");
  const startDate = requiredString(formData, "startDate");
  const endDate = requiredString(formData, "endDate");

  const { data, error } = await supabase.rpc("admin_request_payroll_snapshot", {
    p_source_device_id: sourceDeviceId,
    p_start_date: startDate,
    p_end_date: endDate,
  });

  revalidatePayroll();
  return error ? { error: error.message } : { success: true, commandId: data };
}

export async function addPayrollAdjustment(_previousState: ActionResult | null, formData: FormData): Promise<ActionResult> {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const sourceDeviceId = requiredString(formData, "sourceDeviceId");
  const startDate = requiredString(formData, "startDate");
  const endDate = requiredString(formData, "endDate");
  const barberId = requiredString(formData, "barberId");
  const amount = Number(requiredString(formData, "amount"));
  const reason = requiredString(formData, "reason").trim();

  if (!Number.isFinite(amount)) {
    return { error: "Adjustment amount is invalid." };
  }

  if (!reason) {
    return { error: "Adjustment reason is required." };
  }

  const { data, error } = await supabase.rpc("admin_add_payroll_adjustment", {
    p_source_device_id: sourceDeviceId,
    p_start_date: startDate,
    p_end_date: endDate,
    p_barber_id: barberId,
    p_amount_cents: Math.round(amount * 100),
    p_reason: reason,
  });

  revalidatePayroll();
  return error ? { error: error.message } : { success: true, commandId: data };
}

export async function requestPayrollPayment(_previousState: ActionResult | null, formData: FormData): Promise<ActionResult> {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const sourceDeviceId = requiredString(formData, "sourceDeviceId");
  const startDate = requiredString(formData, "startDate");
  const endDate = requiredString(formData, "endDate");
  const paymentMethod = requiredString(formData, "paymentMethod");
  const paymentReference = optionalString(formData, "paymentReference");
  const notes = optionalString(formData, "notes");

  const { data, error } = await supabase.rpc("admin_request_payroll_payment", {
    p_source_device_id: sourceDeviceId,
    p_start_date: startDate,
    p_end_date: endDate,
    p_payment_method: paymentMethod,
    p_payment_reference: paymentReference,
    p_notes: notes,
  });

  revalidatePayroll();
  return error ? { error: error.message } : { success: true, commandId: data };
}

function requiredString(formData: FormData, key: string) {
  const value = formData.get(key);
  if (typeof value !== "string" || value.trim().length === 0) {
    throw new Error(`${key} is required.`);
  }

  return value.trim();
}

function optionalString(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : null;
}

function revalidatePayroll() {
  revalidatePath("/admin/payroll");
  revalidatePath("/admin/payroll/history");
}
