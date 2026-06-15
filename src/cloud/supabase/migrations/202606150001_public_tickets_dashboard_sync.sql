-- Public Tickets Dashboard sync fields.

alter table public.synced_tickets
  add column if not exists display_ticket_number integer,
  add column if not exists ticket_date date,
  add column if not exists checked_in_at timestamptz;

create index if not exists synced_tickets_dashboard_active_idx
  on public.synced_tickets (ticket_date desc, status, checked_in_at, created_at)
  where appointment_id is null
    and status in ('waiting', 'called', 'in_progress');
