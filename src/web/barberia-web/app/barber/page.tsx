import { AppShell } from "@/components/layout/app-shell";
import { PlaceholderPanel } from "@/components/dashboard/placeholder-panel";
import { requireBarber } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";

export default async function BarberPage() {
  const supabase = await createClient();
  await requireBarber(supabase);

  return (
    <AppShell title="Barber's schedule" variant="barber">
      <PlaceholderPanel title="Today's schedule">
        Assigned appointments and status changes will appear here when role-based policies are active.
      </PlaceholderPanel>
    </AppShell>
  );
}
