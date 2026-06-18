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

function requiredString(formData: FormData, key: string) {
  const value = formData.get(key);
  if (typeof value !== "string" || value.trim().length === 0) {
    throw new Error(`${key} is required.`);
  }

  return value.trim();
}

function revalidatePayroll() {
  revalidatePath("/admin/payroll");
  revalidatePath("/admin/payroll/history");
}

export type DailyBreakdownRow = {
  date: string;
  services: number;
  salesCents: number;
  commissionCents: number;
  earningsCents: number;
  commissionPercentage: number;
};

export async function fetchLineDailyBreakdown(
  barberId: string,
  startDate: string,
  endDate: string,
  periodSalesCents: number,
  periodCommissionCents: number,
): Promise<{ success: true; data: DailyBreakdownRow[] } | { success: false; error: string }> {
  try {
    const supabase = await createClient();
    await requireAdmin(supabase);

    // Expand bounds to cover timezone offsets safely
    const startObj = new Date(`${startDate}T00:00:00Z`);
    startObj.setUTCDate(startObj.getUTCDate() - 1);
    const queryStart = startObj.toISOString().split("T")[0];

    const endObj = new Date(`${endDate}T00:00:00Z`);
    endObj.setUTCDate(endObj.getUTCDate() + 1);
    const queryEnd = endObj.toISOString().split("T")[0];

    const { data: payments, error } = await supabase
      .from("synced_payments")
      .select(`
        amount_cents,
        collected_at,
        synced_tickets!inner ( barber_id )
      `)
      .eq("synced_tickets.barber_id", barberId)
      .is("restore_reverted_at", null)
      .is("synced_tickets.restore_reverted_at", null)
      .gte("collected_at", `${queryStart}T00:00:00Z`)
      .lte("collected_at", `${queryEnd}T23:59:59Z`);

    if (error) throw new Error(error.message);

    const groupedByDate = new Map<string, { services: number; salesCents: number }>();

    for (const payment of payments || []) {
      const parts = new Intl.DateTimeFormat("en-US", {
        timeZone: "America/New_York",
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
      }).formatToParts(new Date(payment.collected_at));
      
      const year = parts.find((p) => p.type === "year")?.value;
      const month = parts.find((p) => p.type === "month")?.value;
      const day = parts.find((p) => p.type === "day")?.value;
      const dateStr = `${year}-${month}-${day}`;

      if (dateStr < startDate || dateStr > endDate) continue;

      const existing = groupedByDate.get(dateStr) || { services: 0, salesCents: 0 };
      existing.services += 1;
      existing.salesCents += payment.amount_cents;
      groupedByDate.set(dateStr, existing);
    }

    const averageCommissionRate = periodSalesCents > 0 ? periodCommissionCents / periodSalesCents : 0;

    const breakdown: DailyBreakdownRow[] = Array.from(groupedByDate.entries())
      .map(([date, stats]) => {
        const commissionCents = Math.round(stats.salesCents * averageCommissionRate);
        return {
          date,
          services: stats.services,
          salesCents: stats.salesCents,
          commissionCents,
          earningsCents: commissionCents,
          commissionPercentage: averageCommissionRate,
        };
      })
      .sort((a, b) => a.date.localeCompare(b.date));

    return { success: true, data: breakdown };
  } catch (err: any) {
    return { success: false, error: err.message };
  }
}
