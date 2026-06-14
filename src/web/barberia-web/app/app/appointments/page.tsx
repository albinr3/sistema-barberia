import { AppShell } from "@/components/layout/app-shell";
import { PlaceholderPanel } from "@/components/dashboard/placeholder-panel";
import { requireCustomer } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";

export default async function AppointmentsPage() {
  const supabase = await createClient();
  await requireCustomer(supabase);

  return (
    <AppShell title="My appointments" variant="customer">
      <PlaceholderPanel title="Customer history">
        Upcoming appointments, history, and actions to cancel or reschedule will live here behind the login.
      </PlaceholderPanel>
    </AppShell>
  );
}
