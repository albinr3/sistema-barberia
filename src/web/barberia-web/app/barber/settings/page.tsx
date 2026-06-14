import { AppShell } from "@/components/layout/app-shell";
import { PlaceholderPanel } from "@/components/dashboard/placeholder-panel";
import { requireBarber } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";

export default async function BarberSettingsPage() {
  const supabase = await createClient();
  await requireBarber(supabase);

  return (
    <AppShell title="Barber's settings" variant="barber">
      <PlaceholderPanel title="Operational profile">
        Availability and profile settings are deferred until the barber permission model is finalized.
      </PlaceholderPanel>
    </AppShell>
  );
}
