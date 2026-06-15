"use server";

import { createClient } from "@/lib/supabase/server";
import { requireAdmin } from "@/lib/auth/profile";
import { revalidatePath } from "next/cache";

export async function dismissSyncConflict(conflictId: string) {
  try {
    const supabase = await createClient();
    await requireAdmin(supabase);

    const { error } = await supabase
      .from("sync_conflicts")
      .update({ status: "resolved", resolved_at: new Date().toISOString() })
      .eq("id", conflictId);

    if (error) {
      return { error: error.message };
    }

    revalidatePath("/admin/sync");
    return { success: true };
  } catch (error: any) {
    return { error: error.message };
  }
}
