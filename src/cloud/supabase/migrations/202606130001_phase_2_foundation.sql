create extension if not exists pgcrypto;

create type public.app_role as enum ('customer', 'barber', 'admin', 'owner');
create type public.appointment_status as enum ('pending', 'confirmed', 'cancelled', 'completed', 'no_show');
create type public.sync_event_status as enum ('received', 'processed', 'failed', 'ignored');
create type public.sync_conflict_status as enum ('open', 'resolved', 'ignored');

create table public.profiles (
  id uuid primary key references auth.users(id) on delete cascade,
  role public.app_role not null default 'customer',
  display_name text,
  phone text,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table public.barbers (
  id uuid primary key default gen_random_uuid(),
  profile_id uuid unique references public.profiles(id) on delete set null,
  display_name text not null,
  station_code text,
  profile_image_path text,
  is_active boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint barbers_station_code_format check (station_code is null or station_code ~ '^B-[0-9]+$')
);

create unique index barbers_active_station_code_unique
  on public.barbers (station_code)
  where is_active and station_code is not null;

create table public.services (
  id uuid primary key default gen_random_uuid(),
  name text not null,
  description text,
  base_price_cents integer not null check (base_price_cents > 0),
  duration_minutes integer not null default 30 check (duration_minutes > 0),
  sort_order integer not null default 0,
  is_active boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table public.barber_services (
  barber_id uuid not null references public.barbers(id) on delete cascade,
  service_id uuid not null references public.services(id) on delete cascade,
  is_active boolean not null default true,
  created_at timestamptz not null default now(),
  primary key (barber_id, service_id)
);

create table public.availability_rules (
  id uuid primary key default gen_random_uuid(),
  barber_id uuid not null references public.barbers(id) on delete cascade,
  day_of_week smallint not null check (day_of_week between 0 and 6),
  starts_at time not null,
  ends_at time not null,
  slot_minutes integer not null default 30 check (slot_minutes > 0),
  is_active boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint availability_rules_time_order check (starts_at < ends_at)
);

create table public.availability_exceptions (
  id uuid primary key default gen_random_uuid(),
  barber_id uuid not null references public.barbers(id) on delete cascade,
  exception_date date not null,
  starts_at time,
  ends_at time,
  is_closed boolean not null default false,
  reason text,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint availability_exceptions_time_order check (
    is_closed or (starts_at is not null and ends_at is not null and starts_at < ends_at)
  )
);

create table public.appointments (
  id uuid primary key default gen_random_uuid(),
  customer_id uuid not null references public.profiles(id) on delete restrict,
  barber_id uuid not null references public.barbers(id) on delete restrict,
  service_id uuid not null references public.services(id) on delete restrict,
  starts_at timestamptz not null,
  ends_at timestamptz not null,
  status public.appointment_status not null default 'pending',
  cancellation_reason text,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint appointments_time_order check (starts_at < ends_at)
);

create index appointments_customer_starts_at_idx on public.appointments (customer_id, starts_at desc);
create index appointments_barber_starts_at_idx on public.appointments (barber_id, starts_at);

create table public.sync_events (
  id uuid primary key default gen_random_uuid(),
  source text not null default 'desktop',
  source_event_id text not null,
  event_type text not null,
  aggregate_type text not null,
  aggregate_id text not null,
  payload jsonb not null default '{}'::jsonb,
  status public.sync_event_status not null default 'received',
  received_at timestamptz not null default now(),
  processed_at timestamptz,
  error_message text,
  unique (source, source_event_id)
);

create table public.sync_conflicts (
  id uuid primary key default gen_random_uuid(),
  sync_event_id uuid references public.sync_events(id) on delete set null,
  conflict_type text not null,
  aggregate_type text not null,
  aggregate_id text not null,
  local_payload jsonb not null default '{}'::jsonb,
  cloud_payload jsonb not null default '{}'::jsonb,
  status public.sync_conflict_status not null default 'open',
  resolution_notes text,
  created_at timestamptz not null default now(),
  resolved_at timestamptz,
  resolved_by uuid references public.profiles(id) on delete set null
);

create table public.audit_log (
  id uuid primary key default gen_random_uuid(),
  actor_id uuid references public.profiles(id) on delete set null,
  action text not null,
  entity_type text not null,
  entity_id text,
  metadata jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default now()
);

create or replace function public.touch_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

create trigger profiles_touch_updated_at
  before update on public.profiles
  for each row execute function public.touch_updated_at();

create trigger barbers_touch_updated_at
  before update on public.barbers
  for each row execute function public.touch_updated_at();

create trigger services_touch_updated_at
  before update on public.services
  for each row execute function public.touch_updated_at();

create trigger availability_rules_touch_updated_at
  before update on public.availability_rules
  for each row execute function public.touch_updated_at();

create trigger availability_exceptions_touch_updated_at
  before update on public.availability_exceptions
  for each row execute function public.touch_updated_at();

create trigger appointments_touch_updated_at
  before update on public.appointments
  for each row execute function public.touch_updated_at();

create or replace function public.current_user_role()
returns public.app_role
language sql
stable
security definer
set search_path = public
as $$
  select role from public.profiles where id = auth.uid()
$$;

create or replace function public.is_admin_or_owner()
returns boolean
language sql
stable
security definer
set search_path = public
as $$
  select coalesce(public.current_user_role() in ('admin', 'owner'), false)
$$;

create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
  insert into public.profiles (id, role, display_name)
  values (new.id, 'customer', coalesce(new.raw_user_meta_data ->> 'display_name', new.email))
  on conflict (id) do nothing;
  return new;
end;
$$;

create trigger on_auth_user_created
  after insert on auth.users
  for each row execute function public.handle_new_user();

alter table public.profiles enable row level security;
alter table public.barbers enable row level security;
alter table public.services enable row level security;
alter table public.barber_services enable row level security;
alter table public.availability_rules enable row level security;
alter table public.availability_exceptions enable row level security;
alter table public.appointments enable row level security;
alter table public.sync_events enable row level security;
alter table public.sync_conflicts enable row level security;
alter table public.audit_log enable row level security;

create policy "Users can read their profile"
  on public.profiles for select
  to authenticated
  using (id = auth.uid() or public.is_admin_or_owner());

create policy "Users can update their profile basics"
  on public.profiles for update
  to authenticated
  using (id = auth.uid())
  with check (id = auth.uid() and role = public.current_user_role());

create policy "Admins can manage profiles"
  on public.profiles for all
  to authenticated
  using (public.is_admin_or_owner())
  with check (public.is_admin_or_owner());

create policy "Authenticated users can read active catalog barbers"
  on public.barbers for select
  to authenticated
  using (is_active or public.is_admin_or_owner());

create policy "Admins can manage barbers"
  on public.barbers for all
  to authenticated
  using (public.is_admin_or_owner())
  with check (public.is_admin_or_owner());

create policy "Authenticated users can read active services"
  on public.services for select
  to authenticated
  using (is_active or public.is_admin_or_owner());

create policy "Admins can manage services"
  on public.services for all
  to authenticated
  using (public.is_admin_or_owner())
  with check (public.is_admin_or_owner());

create policy "Authenticated users can read active barber services"
  on public.barber_services for select
  to authenticated
  using (is_active or public.is_admin_or_owner());

create policy "Admins can manage barber services"
  on public.barber_services for all
  to authenticated
  using (public.is_admin_or_owner())
  with check (public.is_admin_or_owner());

create policy "Authenticated users can read active availability rules"
  on public.availability_rules for select
  to authenticated
  using (is_active or public.is_admin_or_owner());

create policy "Admins can manage availability rules"
  on public.availability_rules for all
  to authenticated
  using (public.is_admin_or_owner())
  with check (public.is_admin_or_owner());

create policy "Authenticated users can read availability exceptions"
  on public.availability_exceptions for select
  to authenticated
  using (true);

create policy "Admins can manage availability exceptions"
  on public.availability_exceptions for all
  to authenticated
  using (public.is_admin_or_owner())
  with check (public.is_admin_or_owner());

create policy "Customers can read their appointments"
  on public.appointments for select
  to authenticated
  using (customer_id = auth.uid() or public.is_admin_or_owner());

create policy "Barbers can read their assigned appointments"
  on public.appointments for select
  to authenticated
  using (
    exists (
      select 1
      from public.barbers b
      where b.id = appointments.barber_id
        and b.profile_id = auth.uid()
    )
  );

create policy "Admins can manage appointments"
  on public.appointments for all
  to authenticated
  using (public.is_admin_or_owner())
  with check (public.is_admin_or_owner());

create policy "Admins can read sync events"
  on public.sync_events for select
  to authenticated
  using (public.is_admin_or_owner());

create policy "Admins can read sync conflicts"
  on public.sync_conflicts for select
  to authenticated
  using (public.is_admin_or_owner());

create policy "Admins can read audit log"
  on public.audit_log for select
  to authenticated
  using (public.is_admin_or_owner());
