-- Manual payroll adjustments are disabled. Legacy columns/tables remain for compatibility.

update public.payroll_admin_commands
set status = 'failed',
    error_message = 'Manual payroll adjustments are no longer supported',
    updated_at = now(),
    applied_at = now()
where command_type = 'adjustment_added'
  and status = 'pending';

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

  if p_command_type = 'adjustment_added' then
    raise exception 'Manual payroll adjustments are no longer supported';
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
  raise exception 'Manual payroll adjustments are no longer supported';
end;
$$;
