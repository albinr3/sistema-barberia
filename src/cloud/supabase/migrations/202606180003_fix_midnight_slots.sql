-- Normalize 23:59 availability ends to effective midnight for slot generation.

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
      (case when ae.ends_at = '23:59:00'::time then '24:00:00'::time else ae.ends_at end) as ends_at,
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
      (case when ar.ends_at = '23:59:00'::time then '24:00:00'::time else ar.ends_at end) as ends_at,
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
  where cs.starts_at > now()
    and not exists (
    select 1
    from public.appointments a
    where a.barber_id = cs.barber_id
      and a.status in ('pending', 'confirmed')
      and a.starts_at < cs.ends_at
      and a.ends_at > cs.starts_at
  )
  order by cs.starts_at, cs.barber_name;
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
      (case when ae.ends_at = '23:59:00'::time then '24:00:00'::time else ae.ends_at end) as ends_at,
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
      (case when ar.ends_at = '23:59:00'::time then '24:00:00'::time else ar.ends_at end) as ends_at,
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