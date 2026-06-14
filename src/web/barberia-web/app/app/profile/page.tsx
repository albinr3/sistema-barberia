import { AppShell } from "@/components/layout/app-shell";
import { PlaceholderPanel } from "@/components/dashboard/placeholder-panel";
import { requireCustomer } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";

export default async function ProfilePage() {
  const supabase = await createClient();
  await requireCustomer(supabase);

  return (
    <AppShell title="Profile" variant="customer">
      <PlaceholderPanel title="Account profile">
        Contact data and customer preferences will be read from Supabase profiles.
      </PlaceholderPanel>
    </AppShell>
  );
}
