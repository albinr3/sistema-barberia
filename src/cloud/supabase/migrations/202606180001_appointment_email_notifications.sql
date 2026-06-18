-- Appointment transactional email queue and cancellation audit.

do $$
begin
  create type public.appointment_email_type as enum (
    'created',
    'reminder_1h',
    'cancelled',
    'no_show'
  );
exception
  when duplicate_object then null;
end $$;

do $$
begin
  create type public.appointment_email_status as enum (
    'pending',
    'processing',
    'sent',
    'failed',
    'cancelled'
  );
exception
  when duplicate_object then null;
end $$;

alter table public.appointments
  add column if not exists cancelled_at timestamptz,
  add column if not exists cancelled_by uuid references public.profiles(id) on delete set null,
  add column if not exists cancelled_by_role public.app_role;

create table if not exists public.appointment_email_jobs (
  id uuid primary key default gen_random_uuid(),
  appointment_id uuid not null references public.appointments(id) on delete cascade,
  email_type public.appointment_email_type not null,
  status public.appointment_email_status not null default 'pending',
  scheduled_for timestamptz not null default now(),
  idempotency_key text not null unique,
  metadata jsonb not null default '{}'::jsonb,
  attempt_count integer not null default 0 check (attempt_count >= 0),
  max_attempts integer not null default 5 check (max_attempts > 0),
  last_attempt_at timestamptz,
  sent_at timestamptz,
  provider_message_id text,
  error_message text,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create index if not exists appointment_email_jobs_due_idx
  on public.appointment_email_jobs (scheduled_for, created_at)
  where status = 'pending';

create index if not exists appointment_email_jobs_appointment_idx
  on public.appointment_email_jobs (appointment_id, email_type, status);

drop trigger if exists appointment_email_jobs_touch_updated_at on public.appointment_email_jobs;
create trigger appointment_email_jobs_touch_updated_at
  before update on public.appointment_email_jobs
  for each row execute function public.touch_updated_at();

alter table public.appointment_email_jobs enable row level security;

drop policy if exists "Admins can read appointment email jobs" on public.appointment_email_jobs;
create policy "Admins can read appointment email jobs"
  on public.appointment_email_jobs for select
  to authenticated
  using (public.is_admin_or_owner());

create or replace function public.enqueue_appointment_email_job(
  p_appointment_id uuid,
  p_email_type public.appointment_email_type,
  p_scheduled_for timestamptz,
  p_idempotency_key text,
  p_metadata jsonb default '{}'::jsonb
)
returns void
language plpgsql
security definer
set search_path = public
as $$
begin
  insert into public.appointment_email_jobs (
    appointment_id,
    email_type,
    scheduled_for,
    idempotency_key,
    metadata
  ) values (
    p_appointment_id,
    p_email_type,
    p_scheduled_for,
    p_idempotency_key,
    coalesce(p_metadata, '{}'::jsonb)
  )
  on conflict (idempotency_key) do update
    set scheduled_for = excluded.scheduled_for,
        metadata = public.appointment_email_jobs.metadata || excluded.metadata,
        status = case
          when public.appointment_email_jobs.status in ('failed', 'cancelled') then 'pending'::public.appointment_email_status
          else public.appointment_email_jobs.status
        end,
        error_message = case
          when public.appointment_email_jobs.status in ('failed', 'cancelled') then null
          else public.appointment_email_jobs.error_message
        end
  where public.appointment_email_jobs.status in ('pending', 'failed', 'cancelled');
end;
$$;

create or replace function public.cancel_pending_appointment_email_jobs(
  p_appointment_id uuid,
  p_email_type public.appointment_email_type default null
)
returns void
language plpgsql
security definer
set search_path = public
as $$
begin
  update public.appointment_email_jobs
  set status = 'cancelled',
      error_message = null
  where appointment_id = p_appointment_id
    and status = 'pending'
    and (p_email_type is null or email_type = p_email_type);
end;
$$;

create or replace function public.enqueue_appointment_email_jobs_from_change()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
declare
  v_reminder_at timestamptz;
  v_reminder_key text;
begin
  if tg_op = 'INSERT' then
    if new.status in ('pending', 'confirmed') then
      perform public.enqueue_appointment_email_job(
        new.id,
        'created',
        now(),
        'appointment:' || new.id::text || ':created',
        jsonb_build_object('triggered_by', 'appointment_insert')
      );

      v_reminder_at := greatest(new.starts_at - interval '1 hour', now());
      v_reminder_key := 'appointment:' || new.id::text || ':reminder_1h:' || extract(epoch from new.starts_at)::bigint::text;

      perform public.enqueue_appointment_email_job(
        new.id,
        'reminder_1h',
        v_reminder_at,
        v_reminder_key,
        jsonb_build_object('starts_at', new.starts_at)
      );
    end if;

    return new;
  end if;

  if tg_op = 'UPDATE' then
    if new.status in ('cancelled', 'completed', 'no_show')
       and old.status is distinct from new.status then
      perform public.cancel_pending_appointment_email_jobs(new.id, 'reminder_1h');
    end if;

    if old.status is distinct from new.status and new.status = 'cancelled' then
      perform public.enqueue_appointment_email_job(
        new.id,
        'cancelled',
        now(),
        'appointment:' || new.id::text || ':cancelled',
        jsonb_build_object(
          'reason', new.cancellation_reason,
          'cancelled_at', new.cancelled_at,
          'cancelled_by_role', new.cancelled_by_role
        )
      );
    end if;

    if old.status is distinct from new.status and new.status = 'no_show' then
      perform public.enqueue_appointment_email_job(
        new.id,
        'no_show',
        now(),
        'appointment:' || new.id::text || ':no_show',
        jsonb_build_object('no_show_at', new.no_show_at)
      );
    end if;

    if new.status in ('pending', 'confirmed')
       and old.starts_at is distinct from new.starts_at then
      perform public.cancel_pending_appointment_email_jobs(new.id, 'reminder_1h');

      v_reminder_at := greatest(new.starts_at - interval '1 hour', now());
      v_reminder_key := 'appointment:' || new.id::text || ':reminder_1h:' || extract(epoch from new.starts_at)::bigint::text;

      perform public.enqueue_appointment_email_job(
        new.id,
        'reminder_1h',
        v_reminder_at,
        v_reminder_key,
        jsonb_build_object(
          'old_starts_at', old.starts_at,
          'starts_at', new.starts_at
        )
      );
    end if;

    return new;
  end if;

  return new;
end;
$$;

drop trigger if exists appointments_enqueue_email_jobs on public.appointments;
create trigger appointments_enqueue_email_jobs
  after insert or update on public.appointments
  for each row execute function public.enqueue_appointment_email_jobs_from_change();

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
      cancellation_reason = coalesce(p_reason, 'Cancelled by customer'),
      cancelled_at = coalesce(cancelled_at, now()),
      cancelled_by = v_customer_id,
      cancelled_by_role = 'customer'::public.app_role
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
  v_admin_role public.app_role;
  v_appointment public.appointments;
begin
  if not public.is_admin_or_owner() then
    raise exception 'Not authorized';
  end if;

  v_admin_id := auth.uid();

  select role into v_admin_role
  from public.profiles
  where id = v_admin_id;

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
      cancellation_reason = coalesce(p_reason, 'Cancelled by administrator'),
      cancelled_at = coalesce(cancelled_at, now()),
      cancelled_by = v_admin_id,
      cancelled_by_role = coalesce(v_admin_role, 'admin'::public.app_role)
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
