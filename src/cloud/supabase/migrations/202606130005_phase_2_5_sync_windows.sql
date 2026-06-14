-- Migración 202606130005_phase_2_5_sync_windows.sql

create table public.sync_devices (
  id uuid primary key default gen_random_uuid(),
  name text not null,
  device_secret_hash text not null,
  is_active boolean not null default true,
  last_sync_at timestamptz,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

-- Extension de tablas existentes (sync_events)
alter table public.sync_events add column if not exists source_device_id uuid references public.sync_devices(id) on delete restrict;

-- Tablas materializadas de POS Desktop
create table public.synced_tickets (
  id uuid primary key default gen_random_uuid(),
  local_ticket_id text not null unique,
  source_device_id uuid not null references public.sync_devices(id) on delete restrict,
  customer_name text,
  barber_id uuid references public.barbers(id) on delete set null,
  status text not null,
  created_at timestamptz not null default now(),
  started_at timestamptz,
  completed_at timestamptz,
  cancelled_at timestamptz,
  updated_at timestamptz not null default now()
);

create table public.synced_ticket_items (
  id uuid primary key default gen_random_uuid(),
  synced_ticket_id uuid not null references public.synced_tickets(id) on delete cascade,
  local_item_id text not null,
  service_id uuid references public.services(id) on delete set null,
  price_cents integer not null,
  unique(synced_ticket_id, local_item_id)
);

create table public.synced_payments (
  id uuid primary key default gen_random_uuid(),
  local_payment_id text not null unique,
  synced_ticket_id uuid not null references public.synced_tickets(id) on delete restrict,
  source_device_id uuid not null references public.sync_devices(id) on delete restrict,
  payment_method text not null check (payment_method in ('cash', 'zelle')),
  amount_cents integer not null,
  collected_at timestamptz not null default now(),
  created_at timestamptz not null default now()
);

-- Tabla de mapeo para catálogo (Desktop -> Cloud)
create table public.desktop_catalog_mappings (
  local_id text not null,
  entity_type text not null check (entity_type in ('service', 'barber')),
  cloud_id uuid not null,
  primary key (local_id, entity_type)
);

-- Triggers para updated_at
create trigger sync_devices_touch_updated_at
  before update on public.sync_devices
  for each row execute function public.touch_updated_at();

create trigger synced_tickets_touch_updated_at
  before update on public.synced_tickets
  for each row execute function public.touch_updated_at();

-- RLS (Row Level Security)
alter table public.sync_devices enable row level security;
alter table public.synced_tickets enable row level security;
alter table public.synced_ticket_items enable row level security;
alter table public.synced_payments enable row level security;
alter table public.desktop_catalog_mappings enable row level security;

create policy "Admins can manage sync devices"
  on public.sync_devices for all
  to authenticated
  using (public.is_admin_or_owner())
  with check (public.is_admin_or_owner());

create policy "Admins can read synced tickets"
  on public.synced_tickets for select
  to authenticated
  using (public.is_admin_or_owner());

create policy "Admins can read synced ticket items"
  on public.synced_ticket_items for select
  to authenticated
  using (public.is_admin_or_owner());

create policy "Admins can read synced payments"
  on public.synced_payments for select
  to authenticated
  using (public.is_admin_or_owner());

create policy "Admins can manage desktop catalog mappings"
  on public.desktop_catalog_mappings for all
  to authenticated
  using (public.is_admin_or_owner())
  with check (public.is_admin_or_owner());
