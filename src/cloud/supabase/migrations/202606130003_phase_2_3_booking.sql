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
    join public.barber_services bs on bs.barber_id = b.id
    where b.id = p_barber_id
      and b.is_active = true
      and bs.service_id = p_service_id
      and bs.is_active = true
  ) then
    raise exception 'Barber is invalid, inactive, or not assigned to this service';
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

create or replace function public.cancel_customer_appointment(
  p_appointment_id uuid,
  p_reason text default null
)
returns public.appointments
language plpgsql
security definer
set search_path = public
as $$
declare
  v_customer_id uuid;
  v_appointment public.appointments;
begin
  v_customer_id := auth.uid();
  if v_customer_id is null then
    raise exception 'Not authenticated';
  end if;

  select * into v_appointment
  from public.appointments
  where id = p_appointment_id;

  if not found then
    raise exception 'Appointment not found';
  end if;

  if v_appointment.customer_id != v_customer_id then
    raise exception 'Not authorized to cancel this appointment';
  end if;

  if v_appointment.status not in ('pending', 'confirmed') then
    raise exception 'Appointment cannot be cancelled (current status: %)', v_appointment.status;
  end if;

  if now() >= v_appointment.starts_at then
    raise exception 'Cannot cancel past or ongoing appointments';
  end if;

  update public.appointments
  set status = 'cancelled',
      cancellation_reason = coalesce(p_reason, 'Cancelled by customer')
  where id = p_appointment_id
  returning * into v_appointment;

  insert into public.audit_log (
    actor_id, action, entity_type, entity_id, metadata
  ) values (
    v_customer_id, 'cancel_appointment', 'appointment', v_appointment.id::text,
    jsonb_build_object('reason', p_reason, 'source', 'customer')
  );

  return v_appointment;
end;
$$;

-- Fix RPC security for availability checking
-- It must be security definer so customers can see overlaps with OTHER customers' appointments
alter function public.get_available_slots(uuid, date, date, uuid) security definer;

