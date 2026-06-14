"use server";

import { revalidatePath } from "next/cache";
import { requireAdmin } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";

export async function saveDesktopCatalogMapping(formData: FormData) {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const entityType = formData.get("entity_type")?.toString();
  const localId = formData.get("local_id")?.toString();
  const cloudId = formData.get("cloud_id")?.toString();

  if (!entityType || !localId || !cloudId) {
    throw new Error("Choose a cloud catalog item before saving the mapping.");
  }

  const { error } = await supabase.from("desktop_catalog_mappings").upsert({
    entity_type: entityType,
    local_id: localId,
    cloud_id: cloudId,
  });

  if (error) {
    throw new Error(error.message || "Could not save catalog mapping.");
  }

  revalidatePath("/admin/sync");
}
