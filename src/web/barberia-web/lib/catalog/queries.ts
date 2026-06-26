import type { SupabaseClient } from "@supabase/supabase-js";
import type {
  AdminCatalogData,
  AvailabilitySlot,
  BarberRow,

  AvailabilityExceptionRow,
  AvailabilityRuleRow,
  ServiceRow,
} from "./types";

function throwIfError(error: { message: string } | null, context: string) {
  if (error) {
    throw new Error(`${context}: ${error.message}`);
  }
}

export async function getAdminCatalogData(supabase: SupabaseClient): Promise<AdminCatalogData> {
  const [barbers, services, availabilityRules, availabilityExceptions] =
    await Promise.all([
      supabase
        .from("barbers")
        .select("id, display_name, station_code, profile_image_path, is_active, is_available_locally")
        .order("display_name", { ascending: true }),
      supabase
        .from("services")
        .select("id, name, description, desktop_price_cents, web_price_cents, duration_minutes, sort_order, is_active")
        .order("sort_order", { ascending: true })
        .order("name", { ascending: true }),
      supabase
        .from("availability_rules")
        .select("id, barber_id, day_of_week, starts_at, ends_at, slot_minutes, is_active")
        .order("barber_id", { ascending: true })
        .order("day_of_week", { ascending: true }),
      supabase
        .from("availability_exceptions")
        .select("id, barber_id, exception_date, starts_at, ends_at, is_closed, reason")
        .order("exception_date", { ascending: true }),
    ]);

  throwIfError(barbers.error, "Unable to load barbers");
  throwIfError(services.error, "Unable to load services");
  throwIfError(availabilityRules.error, "Unable to load availability rules");
  throwIfError(availabilityExceptions.error, "Unable to load availability exceptions");

  return {
    barbers: (barbers.data ?? []) as BarberRow[],
    services: (services.data ?? []) as ServiceRow[],
    availabilityRules: (availabilityRules.data ?? []) as AvailabilityRuleRow[],
    availabilityExceptions: (availabilityExceptions.data ?? []) as AvailabilityExceptionRow[],
  };
}

export async function getAvailabilityPreview(
  supabase: SupabaseClient,
  input: {
    serviceId: string;
    startsOn: string;
    endsOn: string;
    barberId?: string | null;
  },
): Promise<AvailabilitySlot[]> {
  const { data, error } = await supabase.rpc("get_available_slots", {
    service_id: input.serviceId,
    starts_on: input.startsOn,
    ends_on: input.endsOn,
    barber_id: input.barberId || null,
  });

  throwIfError(error, "Unable to preview availability");

  return (data ?? []) as AvailabilitySlot[];
}
