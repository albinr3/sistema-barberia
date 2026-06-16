-- Migration: 202606150004_ticket_admin_cancel.sql

-- Evolve ticket_admin_commands to support different command types and null target_barber_id
alter table public.ticket_admin_commands
  add column command_type text not null default 'reassign' check (command_type in ('reassign', 'cancel'));

alter table public.ticket_admin_commands
  alter column target_barber_id drop not null;

-- Add check constraint: target_barber_id must be null for cancel, and not null for reassign
alter table public.ticket_admin_commands
  add constraint check_target_barber_id_for_type check (
    (command_type = 'reassign' and target_barber_id is not null) or
    (command_type = 'cancel' and target_barber_id is null)
  );

-- RPC admin_cancel_ticket
create or replace function public.admin_cancel_ticket(
  p_synced_ticket_id uuid
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
  v_command_id uuid;
begin
  -- Validate authentication and role
  v_user_id := auth.uid();
  if v_user_id is null or not public.is_admin_or_owner() then
    raise exception 'Unauthorized: Must be an admin or owner to cancel tickets';
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
    raise exception 'Cannot cancel a ticket that belongs to an appointment. Modify the appointment instead.';
  end if;

  if v_ticket_status not in ('waiting', 'called', 'in_progress') then
    raise exception 'Only waiting, called, or in_progress tickets can be cancelled';
  end if;

  -- Insert command
  insert into public.ticket_admin_commands (
    command_type, source_device_id, local_ticket_id, requested_by
  ) values (
    'cancel', v_source_device_id, v_local_ticket_id, v_user_id
  ) returning id into v_command_id;

  -- Audit log
  insert into public.audit_log (
    action,
    actor_id,
    entity_type,
    entity_id,
    metadata
  ) values (
    'admin_ticket_cancel_requested',
    v_user_id,
    'synced_ticket',
    p_synced_ticket_id,
    jsonb_build_object(
      'command_id', v_command_id,
      'local_ticket_id', v_local_ticket_id,
      'source_device_id', v_source_device_id,
      'previous_status', v_ticket_status
    )
  );

  return v_command_id;
end;
$$;
