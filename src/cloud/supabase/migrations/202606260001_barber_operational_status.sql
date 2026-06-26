create table public.barber_operational_status (
  barber_id uuid primary key references public.barbers(id) on delete cascade,
  source_device_id uuid not null references public.sync_devices(id) on delete restrict,
  business_date date not null,
  state text not null,
  clients_served_today integer not null default 0 check (clients_served_today >= 0),
  checked_in_at timestamptz,
  daily_queue_position integer check (daily_queue_position is null or daily_queue_position >= 0),
  daily_arrived_at timestamptz,
  is_checked_in_today boolean not null default false,
  updated_at timestamptz not null
);

create index barber_operational_status_dashboard_idx
  on public.barber_operational_status (business_date desc, is_checked_in_today, daily_queue_position);

alter table public.barber_operational_status enable row level security;

create policy "Admins can read barber operational status"
  on public.barber_operational_status for select
  to authenticated
  using (public.is_admin_or_owner());
