-- Appointment QR, admin rescheduling, and appointment-aware desktop sync.

create or replace function public.generate_appointment_code()
returns text
language plpgsql
volatile
set search_path = public, extensions
as $$
declare
  v_code text;
begin
  loop
    v_code := 'A' || upper(substr(encode(gen_random_bytes(6), 'hex'), 1, 12));
    exit when not exists (
      select 1 from public.appointments where appointment_code = v_code
    );
  end loop;

  return v_code;
end;
$$;

alter table public.appointments
  add column if not exists appointment_code text,
  add column if not exists checked_in_at timestamptz,
  add column if not exists completed_at timestamptz,
  add column if not exists no_show_at timestamptz;

update public.appointments
set appointment_code = public.generate_appointment_code()
where appointment_code is null;

alter table public.appointments
  alter column appointment_code set not null;

create unique index if not exists appointments_appointment_code_unique
  on public.appointments (appointment_code);

do $$
begin
  if not exists (
    select 1
    from pg_constraint
    where conname = 'appointments_appointment_code_format'
  ) then
    alter table public.appointments
      add constraint appointments_appointment_code_format
      check (appointment_code ~ '^A[0-9A-F]{12}$');
  end if;
end $$;

alter table public.synced_tickets
  add column if not exists appointment_id uuid references public.appointments(id) on delete set null;

create table if not exists public.synced_catalog_items (
  source_device_id uuid not null references public.sync_devices(id) on delete cascade,
  entity_type text not null check (entity_type in ('barber', 'service')),
  local_id text not null,
  display_name text not null,
  station_code text,
  price_cents integer,
  is_active boolean not null default true,
  payload jsonb not null default '{}'::jsonb,
  synced_at timestamptz not null default now(),
  primary key (source_device_id, entity_type, local_id)
);

alter table public.synced_catalog_items enable row level security;

drop policy if exists "Admins can read synced catalog items" on public.synced_catalog_items;
create policy "Admins can read synced catalog items"
  on public.synced_catalog_items for select
  to authenticated
  using (public.is_admin_or_owner());

drop policy if exists "Admins can manage synced catalog items" on public.synced_catalog_items;
create policy "Admins can manage synced catalog items"
  on public.synced_catalog_items for all
  to authenticated
  using (public.is_admin_or_owner())
  with check (public.is_admin_or_owner());

create or replace function public.create_appointment(
  p_service_id uuid,
  p_barber_id uuid,
  p_starts_at timestamptz
)
returns public.appointments
language plpgsql
security definer
set search_path = public
as $$
declare
  v_customer_id uuid;
  v_role public.app_role;
  v_duration_minutes integer;
  v_ends_at timestamptz;
  v_appointment public.appointments;
  v_is_available boolean;
begin
  v_customer_id := auth.uid();
  if v_customer_id is null then
    raise exception 'Not authenticated';
  end if;

  select role into v_role from public.profiles where id = v_customer_id;
  if v_role != 'customer' then
    raise exception 'Only customers can book via this function';
  end if;

  select duration_minutes into v_duration_minutes
  from public.services
  where id = p_service_id and is_active = true;

  if not found then
    raise exception 'Service is invalid or inactive';
  end if;

  if not exists (
    select 1
    from public.barbers b
    where b.id = p_barber_id
      and b.is_active = true
  ) then
    raise exception 'Barber is invalid or inactive';
  end if;

  v_ends_at := p_starts_at + make_interval(mins => v_duration_minutes);

  select exists (
    select 1
    from public.get_available_slots(
      p_service_id,
      (p_starts_at at time zone 'America/New_York')::date,
      (p_starts_at at time zone 'America/New_York')::date,
      p_barber_id
    ) s
    where s.starts_at = p_starts_at
  ) into v_is_available;

  if not v_is_available then
    raise exception 'The requested slot is not available';
  end if;

  if exists (
    select 1
    from public.appointments
    where barber_id = p_barber_id
      and status in ('pending', 'confirmed')
      and starts_at < v_ends_at
      and ends_at > p_starts_at
  ) then
    raise exception 'The requested slot overlaps with an existing appointment';
  end if;

  insert into public.appointments (
    customer_id,
    barber_id,
    service_id,
    starts_at,
    ends_at,
    status,
    appointment_code
  ) values (
    v_customer_id,
    p_barber_id,
    p_service_id,
    p_starts_at,
    v_ends_at,
    'confirmed',
    public.generate_appointment_code()
  ) returning * into v_appointment;

  insert into public.audit_log (
    actor_id, action, entity_type, entity_id, metadata
  ) values (
    v_customer_id, 'create_appointment', 'appointment', v_appointment.id::text,
    jsonb_build_object(
      'service_id', p_service_id,
      'barber_id', p_barber_id,
      'starts_at', p_starts_at,
      'ends_at', v_ends_at,
      'appointment_code', v_appointment.appointment_code
    )
  );

  return v_appointment;
end;
$$;

create or replace function public.admin_get_reschedule_slots(
  p_appointment_id uuid,
  p_date date
)
returns table (
  barber_id uuid,
  barber_name text,
  service_id uuid,
  starts_at timestamptz,
  ends_at timestamptz,
  duration_minutes integer
)
language plpgsql
security definer
set search_path = public
as $$
declare
  v_appointment public.appointments;
begin
  if not public.is_admin_or_owner() then
    raise exception 'Not authorized';
  end if;

  select * into v_appointment
  from public.appointments
  where id = p_appointment_id;

  if not found then
    raise exception 'Appointment not found';
  end if;

  if v_appointment.status not in ('pending', 'confirmed') then
    raise exception 'Only pending or confirmed appointments can be rescheduled';
  end if;

  return query
  with selected_service as (
    select s.id, s.duration_minutes
    from public.services s
    where s.id = v_appointment.service_id
      and s.is_active
  ),
  service_barber as (
    select b.id, b.display_name, ss.id as service_id, ss.duration_minutes
    from selected_service ss
    join public.barbers b on b.id = v_appointment.barber_id
    where b.is_active
  ),
  exception_windows as (
    select
      sb.id as barber_id,
      sb.display_name as barber_name,
      sb.service_id,
      sb.duration_minutes,
      p_date as local_day,
      ae.starts_at,
      ae.ends_at,
      sb.duration_minutes as slot_minutes,
      coalesce(ae.is_closed, false) as is_closed
    from service_barber sb
    join public.availability_exceptions ae
      on ae.barber_id = sb.id
     and ae.exception_date = p_date
  ),
  rule_windows as (
    select
      sb.id as barber_id,
      sb.display_name as barber_name,
      sb.service_id,
      sb.duration_minutes,
      p_date as local_day,
      ar.starts_at,
      ar.ends_at,
      ar.slot_minutes,
      false as is_closed
    from service_barber sb
    join public.availability_rules ar
      on ar.barber_id = sb.id
     and ar.day_of_week = extract(dow from p_date)::smallint
     and ar.is_active
    where not exists (
      select 1
      from public.availability_exceptions ae
      where ae.barber_id = sb.id
        and ae.exception_date = p_date
    )
  ),
  availability_windows as (
    select * from exception_windows
    union all
    select * from rule_windows
  ),
  candidate_slots as (
    select
      aw.barber_id,
      aw.barber_name,
      aw.service_id,
      (slot_start.local_ts at time zone 'America/New_York') as starts_at,
      ((slot_start.local_ts + make_interval(mins => aw.duration_minutes)) at time zone 'America/New_York') as ends_at,
      aw.duration_minutes
    from availability_windows aw
    cross join lateral generate_series(
      aw.local_day::timestamp + aw.starts_at,
      aw.local_day::timestamp + aw.ends_at - make_interval(mins => aw.duration_minutes),
      make_interval(mins => aw.slot_minutes)
    ) as slot_start(local_ts)
    where not aw.is_closed
      and aw.starts_at is not null
      and aw.ends_at is not null
      and aw.starts_at < aw.ends_at
  )
  select cs.barber_id, cs.barber_name, cs.service_id, cs.starts_at, cs.ends_at, cs.duration_minutes
  from candidate_slots cs
  where cs.starts_at > now()
    and not exists (
      select 1
      from public.appointments a
      where a.id <> v_appointment.id
        and a.barber_id = cs.barber_id
        and a.status in ('pending', 'confirmed')
        and a.starts_at < cs.ends_at
        and a.ends_at > cs.starts_at
    )
  order by cs.starts_at, cs.barber_name;
end;
$$;

create or replace function public.admin_reschedule_appointment(
  p_appointment_id uuid,
  p_new_starts_at timestamptz
)
returns public.appointments
language plpgsql
security definer
set search_path = public
as $$
declare
  v_admin_id uuid;
  v_appointment public.appointments;
  v_old_starts_at timestamptz;
  v_old_ends_at timestamptz;
  v_duration_minutes integer;
  v_new_ends_at timestamptz;
  v_is_available boolean;
begin
  if not public.is_admin_or_owner() then
    raise exception 'Not authorized';
  end if;

  v_admin_id := auth.uid();

  select * into v_appointment
  from public.appointments
  where id = p_appointment_id;

  if not found then
    raise exception 'Appointment not found';
  end if;

  if v_appointment.status not in ('pending', 'confirmed') then
    raise exception 'Only pending or confirmed appointments can be rescheduled';
  end if;

  if p_new_starts_at <= now() then
    raise exception 'Appointment can only be rescheduled to a future time';
  end if;

  select duration_minutes into v_duration_minutes
  from public.services
  where id = v_appointment.service_id
    and is_active = true;

  if not found then
    raise exception 'Appointment service is invalid or inactive';
  end if;

  v_new_ends_at := p_new_starts_at + make_interval(mins => v_duration_minutes);
  v_old_starts_at := v_appointment.starts_at;
  v_old_ends_at := v_appointment.ends_at;

  select exists (
    select 1
    from public.admin_get_reschedule_slots(
      p_appointment_id,
      (p_new_starts_at at time zone 'America/New_York')::date
    ) s
    where s.starts_at = p_new_starts_at
  ) into v_is_available;

  if not v_is_available then
    raise exception 'The requested reschedule slot is not available';
  end if;

  update public.appointments
  set starts_at = p_new_starts_at,
      ends_at = v_new_ends_at,
      checked_in_at = null,
      completed_at = null,
      no_show_at = null,
      cancellation_reason = null
  where id = p_appointment_id
  returning * into v_appointment;

  insert into public.audit_log (
    actor_id, action, entity_type, entity_id, metadata
  ) values (
    v_admin_id, 'admin_reschedule_appointment', 'appointment', v_appointment.id::text,
    jsonb_build_object(
      'old_starts_at', v_old_starts_at,
      'old_ends_at', v_old_ends_at,
      'new_starts_at', p_new_starts_at,
      'new_ends_at', v_new_ends_at
    )
  );

  return v_appointment;
end;
$$;

create or replace function public.admin_mark_no_show(
  p_appointment_id uuid
)
returns public.appointments
language plpgsql
security definer
set search_path = public
as $$
declare
  v_admin_id uuid;
  v_appointment public.appointments;
begin
  if not public.is_admin_or_owner() then
    raise exception 'Not authorized';
  end if;

  v_admin_id := auth.uid();

  select * into v_appointment
  from public.appointments
  where id = p_appointment_id;

  if not found then
    raise exception 'Appointment not found';
  end if;

  if v_appointment.status not in ('pending', 'confirmed') then
    raise exception 'Cannot mark no-show for status: %', v_appointment.status;
  end if;

  if now() < v_appointment.starts_at + interval '10 minutes' then
    raise exception 'Cannot mark no-show until 10 minutes after start time';
  end if;

  update public.appointments
  set status = 'no_show',
      no_show_at = coalesce(no_show_at, now())
  where id = p_appointment_id
  returning * into v_appointment;

  insert into public.audit_log (
    actor_id, action, entity_type, entity_id, metadata
  ) values (
    v_admin_id, 'admin_mark_no_show', 'appointment', v_appointment.id::text,
    jsonb_build_object()
  );

  return v_appointment;
end;
$$;

create or replace function public.admin_complete_appointment(
  p_appointment_id uuid
)
returns public.appointments
language plpgsql
security definer
set search_path = public
as $$
declare
  v_admin_id uuid;
  v_appointment public.appointments;
begin
  if not public.is_admin_or_owner() then
    raise exception 'Not authorized';
  end if;

  v_admin_id := auth.uid();

  select * into v_appointment
  from public.appointments
  where id = p_appointment_id;

  if not found then
    raise exception 'Appointment not found';
  end if;

  if v_appointment.status != 'confirmed' then
    raise exception 'Only confirmed appointments can be completed';
  end if;

  update public.appointments
  set status = 'completed',
      completed_at = coalesce(completed_at, now())
  where id = p_appointment_id
  returning * into v_appointment;

  insert into public.audit_log (
    actor_id, action, entity_type, entity_id, metadata
  ) values (
    v_admin_id, 'admin_complete_appointment', 'appointment', v_appointment.id::text,
    jsonb_build_object()
  );

  return v_appointment;
end;
$$;

grant execute on function public.admin_get_reschedule_slots(uuid, date) to authenticated;
grant execute on function public.admin_reschedule_appointment(uuid, timestamptz) to authenticated;
