import { AppShell } from "@/components/layout/app-shell";
import { PlaceholderPanel } from "@/components/dashboard/placeholder-panel";
import { requireAdmin } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";

export default async function AdminAppointmentsPage() {
  const supabase = await createClient();
  await requireAdmin(supabase);

  return (
    <AppShell title="Appointments" variant="admin">
      <PlaceholderPanel title="Appointments operation">
        Admin/owner users will be able to reassign, cancel and resolve no-show statuses from here.
      </PlaceholderPanel>
    </AppShell>
  );
}
