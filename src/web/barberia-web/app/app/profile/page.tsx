import { AppShell } from "@/components/layout/app-shell";
import { requireCustomer } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";
import { ProfileForm } from "./profile-form";
import { redirect } from "next/navigation";

export default async function ProfilePage() {
  const supabase = await createClient();
  const sessionUser = await requireCustomer(supabase);

  const { data: { user } } = await supabase.auth.getUser();
  if (!user) {
    redirect("/");
  }

  const { data: profile } = await supabase
    .from("profiles")
    .select("display_name, phone")
    .eq("id", user.id)
    .single();

  const initialData = {
    displayName: profile?.display_name ?? null,
    phone: profile?.phone ?? null,
    email: user.email ?? null,
  };

  return (
    <AppShell title="Profile" variant="customer">
      <div style={{ maxWidth: "800px", margin: "0 auto", padding: "2rem" }}>
        <ProfileForm initialData={initialData} />
      </div>
    </AppShell>
  );
}
