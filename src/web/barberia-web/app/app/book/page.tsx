import { AppShell } from "@/components/layout/app-shell";
import { PlaceholderPanel } from "@/components/dashboard/placeholder-panel";
import { requireCustomer } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";

export default async function BookPage() {
  const supabase = await createClient();
  await requireCustomer(supabase);

  return (
    <AppShell title="Book appointment" variant="customer">
      <PlaceholderPanel title="Authenticated booking">
        Service, barber, date, time and confirmation steps will connect to Supabase availability
        after applying the backend migration.
      </PlaceholderPanel>
    </AppShell>
  );
}
