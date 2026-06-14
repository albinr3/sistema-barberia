import { redirect } from "next/navigation";
import type { SupabaseClient } from "@supabase/supabase-js";
import { appRoles, type AppRole } from "@/types/roles";
import { roleHomePath } from "@/lib/routes";

type ProfileRow = {
  id: string;
  role: string;
  display_name: string | null;
};

export async function requireUser(supabase: SupabaseClient) {
  const {
    data: { user },
  } = await supabase.auth.getUser();

  if (!user) {
    redirect("/");
  }

  return user;
}

export async function getSessionProfile(supabase: SupabaseClient) {
  const user = await requireUser(supabase);

  const { data } = await supabase
    .from("profiles")
    .select("id, role, display_name")
    .eq("id", user.id)
    .maybeSingle<ProfileRow>();

  const role = appRoles.includes(data?.role as AppRole) ? (data?.role as AppRole) : "customer";

  return {
    id: user.id,
    role,
    displayName: data?.display_name ?? user.email ?? null,
  };
}

export async function redirectToRoleHome(supabase: SupabaseClient) {
  const profile = await getSessionProfile(supabase);
  redirect(roleHomePath(profile.role));
}

export async function requireRole(supabase: SupabaseClient, allowedRoles: AppRole[]) {
  const profile = await getSessionProfile(supabase);

  if (!allowedRoles.includes(profile.role)) {
    redirect(roleHomePath(profile.role));
  }

  return profile;
}

export async function requireAdmin(supabase: SupabaseClient) {
  return requireRole(supabase, ["admin", "owner"]);
}

export async function requireCustomer(supabase: SupabaseClient) {
  return requireRole(supabase, ["customer"]);
}

export async function requireBarber(supabase: SupabaseClient) {
  return requireRole(supabase, ["barber"]);
}
