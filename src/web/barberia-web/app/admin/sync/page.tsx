import { AppShell } from "@/components/layout/app-shell";
import { PlaceholderPanel } from "@/components/dashboard/placeholder-panel";
import { requireAdmin } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";

export default async function AdminSyncPage() {
  const supabase = await createClient();
  await requireAdmin(supabase);

  return (
    <AppShell title="Sync" variant="admin">
      <PlaceholderPanel title="Desktop sync health">
        Sync events and conflicts are scaffolded as contract surface; POS details remain
        deferred.
      </PlaceholderPanel>
    </AppShell>
  );
}
