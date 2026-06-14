import { redirectToRoleHome } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";

export default async function AppPage() {
  const supabase = await createClient();
  await redirectToRoleHome(supabase);
}
