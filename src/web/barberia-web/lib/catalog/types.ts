export type BarberRow = {
  id: string;
  display_name: string;
  station_code: string | null;
  profile_image_path: string | null;
  is_active: boolean;
};

export type ServiceRow = {
  id: string;
  name: string;
  description: string | null;
  base_price_cents: number;
  duration_minutes: number;
  sort_order: number;
  is_active: boolean;
};

export type AvailabilityRuleRow = {
  id: string;
  barber_id: string;
  day_of_week: number;
  starts_at: string;
  ends_at: string;
  slot_minutes: number;
  is_active: boolean;
};

export type AvailabilityExceptionRow = {
  id: string;
  barber_id: string;
  exception_date: string;
  starts_at: string | null;
  ends_at: string | null;
  is_closed: boolean;
  reason: string | null;
};

export type AvailabilitySlot = {
  barber_id: string;
  barber_name: string;
  service_id: string;
  starts_at: string;
  ends_at: string;
  duration_minutes: number;
};

export type AdminCatalogData = {
  barbers: BarberRow[];
  services: ServiceRow[];
  availabilityRules: AvailabilityRuleRow[];
  availabilityExceptions: AvailabilityExceptionRow[];
};
