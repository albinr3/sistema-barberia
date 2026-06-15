"use server";

import { createClient } from "@/lib/supabase/server";
import { revalidatePath } from "next/cache";

export async function updateProfile(formData: FormData) {
  const supabase = await createClient();
  const { data: { user } } = await supabase.auth.getUser();

  if (!user) {
    return { error: "No user found" };
  }

  const displayName = formData.get("displayName") as string;
  const phone = formData.get("phone") as string;

  if (!displayName) {
    return { error: "Name is required" };
  }

  const { error } = await supabase
    .from("profiles")
    .update({
      display_name: displayName,
      phone: phone,
    })
    .eq("id", user.id);

  if (error) {
    console.error("Error updating profile:", error);
    return { error: "Failed to update profile" };
  }

  revalidatePath("/app/profile");
  return { success: true };
}
