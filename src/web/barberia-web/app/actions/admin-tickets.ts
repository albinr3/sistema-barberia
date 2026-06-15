"use server";

import { revalidatePath } from "next/cache";
import { createClient } from "@/lib/supabase/server";
import { requireAdmin } from "@/lib/auth/profile";

export async function adminReassignTicket(syncedTicketId: string, targetBarberId: string) {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const { data, error } = await supabase.rpc("admin_reassign_ticket", {
    p_synced_ticket_id: syncedTicketId,
    p_target_barber_id: targetBarberId,
  });

  if (error) {
    console.error("admin_reassign_ticket error", error);
    return { error: error.message || "Failed to reassign ticket" };
  }

  revalidatePath("/admin/tickets");
  revalidatePath("/tickets-dashboard");
  return { success: true, commandId: data };
}
