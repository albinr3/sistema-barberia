-- Payroll web commands and desktop-authoritative snapshots.

alter table public.sync_devices
  add column if not exists pending_outbox_count integer not null default 0;

create table if not exists public.synced_payroll_periods (
  id uuid primary key default gen_random_uuid(),
  source_device_id uuid not null references public.sync_devices(id) on delete restrict,
  local_period_id uuid not null,
  start_date date not null,
  end_date date not null,
  state text not null check (state in ('draft', 'paid')),
  total_services integer not null default 0,
  total_commission_cents bigint not null default 0,
  total_adjustments_cents bigint not null default 0,
  total_to_pay_cents bigint not null default 0,
  payment_method text check (payment_method is null or payment_method in ('cash', 'transfer', 'other')),
  payment_reference text,
  notes text,
  generated_at timestamptz not null,
  paid_at timestamptz,
  loaded_at timestamptz not null,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  unique (source_device_id, start_date, end_date)
);

create table if not exists public.synced_payroll_lines (
  id uuid primary key default gen_random_uuid(),
  payroll_period_id uuid not null references public.synced_payroll_periods(id) on delete cascade,
  local_line_id uuid not null,
  barber_id uuid,
  barber_name text not null,
  station_number integer,
  closed_services_count integer not null default 0,
  sales_generated_cents bigint not null default 0,
  commission_cents bigint not null default 0,
  adjustments_cents bigint not null default 0,
  total_cents bigint not null default 0,
  created_at timestamptz not null default now(),
  unique (payroll_period_id, local_line_id)
);

create table if not exists public.synced_payroll_adjustments (
  id uuid primary key default gen_random_uuid(),
  payroll_period_id uuid not null references public.synced_payroll_periods(id) on delete cascade,
  local_adjustment_id uuid not null,
  barber_id uuid,
  amount_cents bigint not null,
  reason text not null,
  created_at timestamptz not null,
  unique (payroll_period_id, local_adjustment_id)
);

create table if not exists public.payroll_admin_commands (
  id uuid primary key default gen_random_uuid(),
  source_device_id uuid not null references public.sync_devices(id) on delete restrict,
  command_type text not null check (command_type in ('snapshot_requested', 'adjustment_added', 'pay_requested')),
  start_date date not null,
  end_date date not null,
  payload jsonb not null default '{}'::jsonb,
  requested_by uuid not null references auth.users(id),
  status text not null check (status in ('pending', 'applied', 'failed')) default 'pending',
  error_message text,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  applied_at timestamptz
);

create index if not exists idx_synced_payroll_periods_device_range
  on public.synced_payroll_periods(source_device_id, start_date desc);

create index if not exists idx_synced_payroll_lines_period
  on public.synced_payroll_lines(payroll_period_id);

create index if not exists idx_synced_payroll_adjustments_period
  on public.synced_payroll_adjustments(payroll_period_id);

create index if not exists idx_payroll_admin_commands_device_status
  on public.payroll_admin_commands(source_device_id, status, created_at);

create unique index if not exists idx_payroll_admin_commands_one_pending
  on public.payroll_admin_commands(source_device_id, start_date, end_date)
  where status = 'pending';

create trigger synced_payroll_periods_touch_updated_at
  before update on public.synced_payroll_periods
  for each row execute function public.touch_updated_at();

create trigger payroll_admin_commands_touch_updated_at
  before update on public.payroll_admin_commands
  for each row execute function public.touch_updated_at();

alter table public.synced_payroll_periods enable row level security;
alter table public.synced_payroll_lines enable row level security;
alter table public.synced_payroll_adjustments enable row level security;
alter table public.payroll_admin_commands enable row level security;

create policy "Admins can read synced payroll periods"
  on public.synced_payroll_periods for select
  to authenticated
  using (public.is_admin_or_owner());

create policy "Admins can read synced payroll lines"
  on public.synced_payroll_lines for select
  to authenticated
  using (public.is_admin_or_owner());

create policy "Admins can read synced payroll adjustments"
  on public.synced_payroll_adjustments for select
  to authenticated
  using (public.is_admin_or_owner());

create policy "Admins can read payroll commands"
  on public.payroll_admin_commands for select
  to authenticated
  using (public.is_admin_or_owner());

create or replace function public.insert_payroll_admin_command(
  p_source_device_id uuid,
  p_command_type text,
  p_start_date date,
  p_end_date date,
  p_payload jsonb
) returns uuid
language plpgsql
security definer set search_path = public
as $$
declare
  v_user_id uuid;
  v_command_id uuid;
begin
  v_user_id := auth.uid();
  if v_user_id is null or not public.is_admin_or_owner() then
    raise exception 'Unauthorized: Must be an admin or owner to manage payroll';
  end if;

  if p_end_date <= p_start_date then
    raise exception 'Payroll period date range is invalid';
  end if;

  if not exists (
    select 1 from public.sync_devices
    where id = p_source_device_id and is_active
  ) then
    raise exception 'Active sync device was not found';
  end if;

  if exists (
    select 1
    from public.payroll_admin_commands
    where source_device_id = p_source_device_id
      and start_date = p_start_date
      and end_date = p_end_date
      and status = 'pending'
  ) then
    raise exception 'A payroll command is already pending for this period';
  end if;

  insert into public.payroll_admin_commands (
    source_device_id,
    command_type,
    start_date,
    end_date,
    payload,
    requested_by
  ) values (
    p_source_device_id,
    p_command_type,
    p_start_date,
    p_end_date,
    coalesce(p_payload, '{}'::jsonb),
    v_user_id
  ) returning id into v_command_id;

  insert into public.audit_log (
    action,
    actor_id,
    entity_type,
    entity_id,
    metadata
  ) values (
    'admin_payroll_' || p_command_type,
    v_user_id,
    'payroll_admin_command',
    v_command_id::text,
    jsonb_build_object(
      'source_device_id', p_source_device_id,
      'start_date', p_start_date,
      'end_date', p_end_date
    ) || coalesce(p_payload, '{}'::jsonb)
  );

  return v_command_id;
end;
$$;

create or replace function public.admin_request_payroll_snapshot(
  p_source_device_id uuid,
  p_start_date date,
  p_end_date date
) returns uuid
language plpgsql
security definer set search_path = public
as $$
begin
  return public.insert_payroll_admin_command(
    p_source_device_id,
    'snapshot_requested',
    p_start_date,
    p_end_date,
    jsonb_build_object(
      'start_date', p_start_date,
      'end_date', p_end_date
    )
  );
end;
$$;

create or replace function public.admin_add_payroll_adjustment(
  p_source_device_id uuid,
  p_start_date date,
  p_end_date date,
  p_barber_id uuid,
  p_amount_cents bigint,
  p_reason text
) returns uuid
language plpgsql
security definer set search_path = public
as $$
begin
  if p_barber_id is null then
    raise exception 'Barber is required';
  end if;

  if p_reason is null or btrim(p_reason) = '' then
    raise exception 'Adjustment reason is required';
  end if;

  if not exists (select 1 from public.barbers where id = p_barber_id) then
    raise exception 'Barber was not found';
  end if;

  return public.insert_payroll_admin_command(
    p_source_device_id,
    'adjustment_added',
    p_start_date,
    p_end_date,
    jsonb_build_object(
      'start_date', p_start_date,
      'end_date', p_end_date,
      'barber_id', p_barber_id,
      'amount_cents', p_amount_cents,
      'reason', btrim(p_reason)
    )
  );
end;
$$;

create or replace function public.admin_request_payroll_payment(
  p_source_device_id uuid,
  p_start_date date,
  p_end_date date,
  p_payment_method text default 'cash',
  p_payment_reference text default null,
  p_notes text default null
) returns uuid
language plpgsql
security definer set search_path = public
as $$
declare
  v_last_sync_at timestamptz;
  v_pending_outbox_count integer;
  v_today date;
  v_payment_method text;
begin
  select last_sync_at, pending_outbox_count
  into v_last_sync_at, v_pending_outbox_count
  from public.sync_devices
  where id = p_source_device_id and is_active;

  if not found then
    raise exception 'Active sync device was not found';
  end if;

  if v_last_sync_at is null or v_last_sync_at < now() - interval '15 minutes' then
    raise exception 'Desktop sync is stale. Payroll payment cannot be requested yet';
  end if;

  if coalesce(v_pending_outbox_count, 0) <> 0 then
    raise exception 'Desktop has pending sync events. Payroll payment cannot be requested yet';
  end if;

  v_today := (now() at time zone 'America/New_York')::date;
  if p_end_date > v_today then
    raise exception 'Payroll period has not closed yet';
  end if;

  if exists (
    select 1 from public.synced_payroll_periods
    where source_device_id = p_source_device_id
      and start_date = p_start_date
      and end_date = p_end_date
      and state = 'paid'
  ) then
    raise exception 'Payroll period is already paid';
  end if;

  if not exists (
    select 1 from public.synced_payroll_periods
    where source_device_id = p_source_device_id
      and start_date = p_start_date
      and end_date = p_end_date
      and state = 'draft'
  ) then
    raise exception 'A current desktop payroll snapshot is required before payment';
  end if;

  v_payment_method := lower(coalesce(nullif(btrim(p_payment_method), ''), 'cash'));
  if v_payment_method not in ('cash', 'transfer', 'other') then
    raise exception 'Payroll payment method is invalid';
  end if;

  return public.insert_payroll_admin_command(
    p_source_device_id,
    'pay_requested',
    p_start_date,
    p_end_date,
    jsonb_build_object(
      'start_date', p_start_date,
      'end_date', p_end_date,
      'payment_method', v_payment_method,
      'payment_reference', nullif(btrim(p_payment_reference), ''),
      'notes', nullif(btrim(p_notes), '')
    )
  );
end;
$$;
