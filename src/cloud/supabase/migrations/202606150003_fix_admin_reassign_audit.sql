create or replace function public.admin_reassign_ticket(
  p_synced_ticket_id uuid,
  p_target_barber_id uuid
) returns uuid
language plpgsql
security definer set search_path = public
as $$
declare
  v_user_id uuid;
  v_local_ticket_id uuid;
  v_source_device_id text;
  v_ticket_status text;
  v_appointment_id uuid;
  v_barber_is_active boolean;
  v_barber_available_locally boolean;
  v_command_id uuid;
begin
  -- Validate authentication and role
  v_user_id := auth.uid();
  if v_user_id is null or not public.is_admin_or_owner() then
    raise exception 'Unauthorized: Must be an admin or owner to reassign tickets';
  end if;

  -- Get ticket info
  select local_ticket_id, source_device_id, status, appointment_id
  into v_local_ticket_id, v_source_device_id, v_ticket_status, v_appointment_id
  from public.synced_tickets
  where id = p_synced_ticket_id;

  if not found then
    raise exception 'Ticket not found';
  end if;

  if v_appointment_id is not null then
    raise exception 'Cannot reassign a ticket that belongs to an appointment. Modify the appointment instead.';
  end if;

  if v_ticket_status not in ('waiting', 'called') then
    raise exception 'Only waiting or called tickets can be reassigned';
  end if;

  -- Get barber info
  select is_active, is_available_locally
  into v_barber_is_active, v_barber_available_locally
  from public.barbers
  where id = p_target_barber_id;

  if not found then
    raise exception 'Barber not found';
  end if;

  if not v_barber_is_active or not coalesce(v_barber_available_locally, true) then
    raise exception 'Target barber must be active and available locally';
  end if;

  -- Insert command
  insert into public.ticket_admin_commands (
    source_device_id, local_ticket_id, target_barber_id, requested_by
  ) values (
    v_source_device_id, v_local_ticket_id, p_target_barber_id, v_user_id
  ) returning id into v_command_id;

  -- Audit log (Fixed columns)
  insert into public.audit_log (
    action,
    actor_id,
    entity_type,
    entity_id,
    metadata
  ) values (
    'admin_ticket_reassign_requested',
    v_user_id,
    'synced_ticket',
    p_synced_ticket_id,
    jsonb_build_object(
      'command_id', v_command_id,
      'local_ticket_id', v_local_ticket_id,
      'source_device_id', v_source_device_id,
      'target_barber_id', p_target_barber_id,
      'previous_status', v_ticket_status
    )
  );

  return v_command_id;
end;
$$;
