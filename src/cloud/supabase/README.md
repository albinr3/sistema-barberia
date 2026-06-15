# Supabase Cloud

Fase 2 cloud foundation for Auth, PostgreSQL, RLS, booking and sync contracts.

## Current Scope

- `profiles` extends Supabase Auth users with domain profile data only.
- Catalog and appointment tables are created with RLS enabled from the first migration.
- Phase 2.2 adds catalog/availability indexes and the `public.get_available_slots` RPC for authenticated availability preview and future booking.
- Sync tables now include desktop devices, POS ticket/payment materialization, local catalog snapshots, and admin ticket commands.
- Appointments include a stable `appointment_code` used as the customer QR payload for Barber Panel and Cash Box flow.

## Local Use

Install the Supabase CLI, then from this folder run migrations against a local or linked Supabase project after project credentials are approved.

Do not expose service-role or secret keys to the web app. Client code must use the publishable key and RLS. Legacy `anon` keys are supported only as a fallback for older Supabase projects.

## Availability Contract

`public.get_available_slots(service_id uuid, starts_on date, ends_on date, barber_id uuid default null)` returns active
bookable future slots in `America/New_York`. It filters out any past slots (where start time <= now), inactive services, and inactive barbers.
It applies date exceptions over weekly rules, and removes slots that overlap `pending` or `confirmed`
appointments.

Customers create appointments via `public.create_appointment`, which rejects any attempt to book a slot in the past.

Admins reschedule future `pending` or `confirmed` appointments through:

- `public.admin_get_reschedule_slots(p_appointment_id uuid, p_date date)`
- `public.admin_reschedule_appointment(p_appointment_id uuid, p_new_starts_at timestamptz)`

Rescheduling keeps the current barber and service, validates the same availability rules, excludes the appointment being moved from overlap checks, and preserves the QR `appointment_code`.

## Desktop Sync Contract

Desktop devices authenticate to Edge Functions with `x-device-id` and bearer `deviceSecret`.

- `sync-changes` returns catalog changes, appointment changes with `appointment_code`, customer/barber/service summaries, and pending `ticket_admin_commands`.
- `sync-events` accepts `catalog.snapshot`, `appointment.checked_in`, `appointment.no_show`, `appointment.completed`, POS ticket/payment events, `ticket_admin_command.applied`, `ticket_admin_command.failed` and `sync.conflict`.
- `sync-changes` and `sync-events` run with `verify_jwt = false`; they authenticate desktop devices inside the function using `x-device-id` and `Authorization: Bearer <deviceSecret>`.
- `ticket_admin_commands` orchestrate cross-device ticket operations initiated by admins on the web, executed locally by the Desktop authority, and acknowledged back.
