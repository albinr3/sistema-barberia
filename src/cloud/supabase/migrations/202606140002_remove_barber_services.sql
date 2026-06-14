-- 1. Replace get_available_slots to ignore barber_services
create or replace function public.get_available_slots(
  service_id uuid,
  starts_on date,
  ends_on date,
  barber_id uuid default null
)
returns table (
  barber_id uuid,
  barber_name text,
  service_id uuid,
  starts_at timestamptz,
  ends_at timestamptz,
  duration_minutes integer
)
language sql
stable
security invoker
set search_path = public
as $$
  with selected_service as (
    select s.id, s.duration_minutes
    from public.services s
    where s.id = $1
      and s.is_active
  ),
  service_barbers as (
    select b.id, b.display_name, ss.id as service_id, ss.duration_minutes
    from selected_service ss
    cross join public.barbers b
    where b.is_active
      and ($4 is null or b.id = $4)
  ),
  target_days as (
    select generate_series($2, $3, interval '1 day')::date as local_day
  ),
  exception_windows as (
    select
      sb.id as barber_id,
      sb.display_name as barber_name,
      sb.service_id,
      sb.duration_minutes,
      td.local_day,
      ae.starts_at,
      ae.ends_at,
      sb.duration_minutes as slot_minutes,
      coalesce(ae.is_closed, false) as is_closed
    from service_barbers sb
    join target_days td on true
    join public.availability_exceptions ae
      on ae.barber_id = sb.id
     and ae.exception_date = td.local_day
  ),
  rule_windows as (
    select
      sb.id as barber_id,
      sb.display_name as barber_name,
      sb.service_id,
      sb.duration_minutes,
      td.local_day,
      ar.starts_at,
      ar.ends_at,
      ar.slot_minutes,
      false as is_closed
    from service_barbers sb
    join target_days td on true
    join public.availability_rules ar
      on ar.barber_id = sb.id
     and ar.day_of_week = extract(dow from td.local_day)::smallint
     and ar.is_active
    where not exists (
      select 1
      from public.availability_exceptions ae
      where ae.barber_id = sb.id
        and ae.exception_date = td.local_day
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
  where not exists (
    select 1
    from public.appointments a
    where a.barber_id = cs.barber_id
      and a.status in ('pending', 'confirmed')
      and a.starts_at < cs.ends_at
      and a.ends_at > cs.starts_at
  )
  order by cs.starts_at, cs.barber_name;
$$;


-- 2. Replace create_appointment to ignore barber_services
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
  -- 1. Validate Authentication & Role
  v_customer_id := auth.uid();
  if v_customer_id is null then
    raise exception 'Not authenticated';
  end if;

  select role into v_role from public.profiles where id = v_customer_id;
  if v_role != 'customer' then
    raise exception 'Only customers can book via this function';
  end if;

  -- 2. Validate Service and Barber active
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

  -- 3. Validate Availability using the existing RPC logic
  -- Note: We check if the requested slot is in the available slots for that date
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

  -- 4. Double-check Overlap (though get_available_slots already checked, this is for concurrency)
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

  -- 5. Insert Appointment
  insert into public.appointments (
    customer_id,
    barber_id,
    service_id,
    starts_at,
    ends_at,
    status
  ) values (
    v_customer_id,
    p_barber_id,
    p_service_id,
    p_starts_at,
    v_ends_at,
    'confirmed' -- En MVP creamos confirmed directamente
  ) returning * into v_appointment;

  -- 6. Audit Log
  insert into public.audit_log (
    actor_id, action, entity_type, entity_id, metadata
  ) values (
    v_customer_id, 'create_appointment', 'appointment', v_appointment.id::text,
    jsonb_build_object(
      'service_id', p_service_id,
      'barber_id', p_barber_id,
      'starts_at', p_starts_at,
      'ends_at', v_ends_at
    )
  );

  return v_appointment;
end;
$$;


-- 3. Replace admin_reassign_appointment to ignore barber_services
create or replace function public.admin_reassign_appointment(
  p_appointment_id uuid,
  p_new_barber_id uuid
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
    raise exception 'Cannot reassign an appointment with status: %', v_appointment.status;
  end if;

  if v_appointment.barber_id = p_new_barber_id then
    return v_appointment; -- No change
  end if;

  -- Validate new barber active
  if not exists (
    select 1
    from public.barbers b
    where b.id = p_new_barber_id
      and b.is_active = true
  ) then
    raise exception 'New barber is invalid or inactive';
  end if;

  -- Validate overlap for new barber
  if exists (
    select 1
    from public.appointments
    where barber_id = p_new_barber_id
      and status in ('pending', 'confirmed')
      and starts_at < v_appointment.ends_at
      and ends_at > v_appointment.starts_at
  ) then
    raise exception 'Reassignment fails due to overlap with an existing appointment for the new barber';
  end if;

  update public.appointments
  set barber_id = p_new_barber_id
  where id = p_appointment_id
  returning * into v_appointment;

  insert into public.audit_log (
    actor_id, action, entity_type, entity_id, metadata
  ) values (
    v_admin_id, 'admin_reassign_appointment', 'appointment', v_appointment.id::text,
    jsonb_build_object('new_barber_id', p_new_barber_id)
  );

  return v_appointment;
end;
$$;

-- 4. Drop barber_services policies and table
drop table if exists public.barber_services cascade;
