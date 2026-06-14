create or replace function public.admin_cancel_appointment(
  p_appointment_id uuid,
  p_reason text default null
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
    raise exception 'Appointment cannot be cancelled (current status: %)', v_appointment.status;
  end if;

  update public.appointments
  set status = 'cancelled',
      cancellation_reason = coalesce(p_reason, 'Cancelled by administrator')
  where id = p_appointment_id
  returning * into v_appointment;

  insert into public.audit_log (
    actor_id, action, entity_type, entity_id, metadata
  ) values (
    v_admin_id, 'admin_cancel_appointment', 'appointment', v_appointment.id::text,
    jsonb_build_object('reason', p_reason)
  );

  return v_appointment;
end;
$$;

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

  -- Validate new barber active and assigned to service
  if not exists (
    select 1
    from public.barbers b
    join public.barber_services bs on bs.barber_id = b.id
    where b.id = p_new_barber_id
      and b.is_active = true
      and bs.service_id = v_appointment.service_id
      and bs.is_active = true
  ) then
    raise exception 'New barber is invalid, inactive, or not assigned to this service';
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
  set status = 'no_show'
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
  set status = 'completed'
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
