import { AppShell } from "@/components/layout/app-shell";
import { PlaceholderPanel } from "@/components/dashboard/placeholder-panel";
import { requireAdmin } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";

export default async function AdminPage() {
  const supabase = await createClient();
  await requireAdmin(supabase);

  return (
    <AppShell title="Admin dashboard" variant="admin">
      <PlaceholderPanel title="Operational summary">
        Appointments, catalog status, availability, conflicts and sync health will be displayed as
        dense administrative views.
      </PlaceholderPanel>
    </AppShell>
  );
}
