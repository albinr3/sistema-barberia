-- Migration: 202606180002_desktop_restore_reverts.sql

create table if not exists public.desktop_restore_batches (
  id uuid primary key,
  source_device_id uuid not null references public.sync_devices(id) on delete restrict,
  restored_at timestamptz not null,
  backup_file_name text,
  backup_size_bytes bigint,
  safety_backup_path text,
  ticket_count integer not null default 0,
  payment_count integer not null default 0,
  reverted_ticket_count integer not null default 0,
  reverted_payment_count integer not null default 0,
  sync_event_id uuid references public.sync_events(id) on delete set null,
  created_at timestamptz not null default now()
);

alter table public.synced_tickets
  add column if not exists restore_reverted_at timestamptz,
  add column if not exists restore_reverted_by uuid references public.desktop_restore_batches(id) on delete set null,
  add column if not exists restore_revert_reason text;

alter table public.synced_ticket_items
  add column if not exists restore_reverted_at timestamptz,
  add column if not exists restore_reverted_by uuid references public.desktop_restore_batches(id) on delete set null,
  add column if not exists restore_revert_reason text;

alter table public.synced_payments
  add column if not exists restore_reverted_at timestamptz,
  add column if not exists restore_reverted_by uuid references public.desktop_restore_batches(id) on delete set null,
  add column if not exists restore_revert_reason text;

create index if not exists synced_tickets_active_restore_idx
  on public.synced_tickets (source_device_id, ticket_date desc, status, checked_in_at, created_at)
  where restore_reverted_at is null;

create index if not exists synced_payments_active_restore_idx
  on public.synced_payments (source_device_id, collected_at)
  where restore_reverted_at is null;

create index if not exists synced_ticket_items_active_restore_idx
  on public.synced_ticket_items (synced_ticket_id)
  where restore_reverted_at is null;

alter table public.desktop_restore_batches enable row level security;

create policy "Admins can read desktop restore batches"
  on public.desktop_restore_batches for select
  to authenticated
  using (public.is_admin_or_owner());
