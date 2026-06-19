# Supabase Cloud

Fase 2 cloud foundation for Auth, PostgreSQL, RLS, booking and sync contracts.

## Current Scope

- `profiles` extends Supabase Auth users with domain profile data only.
- Catalog and appointment tables are created with RLS enabled from the first migration.
- Phase 2.2 adds catalog/availability indexes and the `public.get_available_slots` RPC for authenticated availability preview and future booking.
- Sync tables now include desktop devices, POS ticket/payment materialization, local catalog snapshots, admin ticket commands, and desktop-authoritative payroll commands/snapshots.
- Appointments include a stable `appointment_code` used as the customer QR payload for Barber Panel and Cash Box flow.
- Appointment email jobs queue transactional customer emails for confirmation, 1-hour reminders, cancellations and no-shows.

## Local Use

Install the Supabase CLI, then from this folder run migrations against a local or linked Supabase project after project credentials are approved.

Do not expose service-role or secret keys to the web app. Client code must use the publishable key and RLS. Legacy `anon` keys are supported only as a fallback for older Supabase projects.

## Availability Contract

`public.get_available_slots(service_id uuid, starts_on date, ends_on date, barber_id uuid default null)` returns active
bookable future slots in `America/New_York`. It filters out any past slots (where start time <= now), inactive services, and inactive barbers.
It applies date exceptions over weekly rules, and removes slots that overlap `pending` or `confirmed`
appointments.
When availability is stored with `ends_at = 23:59:00`, the RPC treats that value as effective midnight for slot generation so the last valid slot before closing is not dropped.

Customers create appointments via `public.create_appointment`, which rejects any attempt to book a slot in the past.

Admins reschedule future `pending` or `confirmed` appointments through:

- `public.admin_get_reschedule_slots(p_appointment_id uuid, p_date date)`
- `public.admin_reschedule_appointment(p_appointment_id uuid, p_new_starts_at timestamptz)`

Rescheduling keeps the current barber and service, validates the same availability rules, excludes the appointment being moved from overlap checks, and preserves the QR `appointment_code`.
The same `23:59:00` normalization is applied to admin reschedule slot generation.

## Appointment Emails

Customer appointment emails are sent by the `appointment-emails` Edge Function through Resend. PostgreSQL triggers enqueue
jobs in `appointment_email_jobs` when appointments are created, cancelled, rescheduled or marked `no_show`.

Required Edge Function secrets:

- `RESEND_API_KEY`
- `APPOINTMENT_EMAIL_FROM`
- `PUBLIC_SITE_URL`
- `APPOINTMENT_EMAIL_INTERNAL_SECRET`

Production setup checklist:

1. Create a Resend account.
2. Verify the sending domain in Resend.
3. Configure DNS records requested by Resend, including DKIM and any SPF/return-path records.
4. Add a DMARC record for better deliverability.
5. Create a Resend API key.
6. Set `APPOINTMENT_EMAIL_FROM`, for example `Master Clips <appointments@example.com>`.
7. Set the four Edge Function secrets in Supabase.
8. Deploy `appointment-emails` with JWT verification disabled.
9. Run `scripts/setup-appointment-email-cron.sql` after replacing placeholders.
10. Send real tests to Gmail and Outlook/iCloud inboxes and check spam/promotions folders.

## Desktop Sync Contract

Desktop devices authenticate to Edge Functions with `x-device-id` and bearer `deviceSecret`.

- `sync-changes` returns catalog changes, appointment changes with `appointment_code`, customer/barber/service summaries, the current New Jersey operational-day active appointments as a backfill window, pending `ticket_admin_commands`, and pending `payroll_admin_commands`.
- `sync-events` accepts `catalog.snapshot`, `desktop.sync_heartbeat`, `payroll.snapshot`, appointment events, POS ticket/payment events, ticket command acks, payroll command acks and `sync.conflict`.
- `sync-changes` and `sync-events` run with `verify_jwt = false`; they authenticate desktop devices inside the function using `x-device-id` and `Authorization: Bearer <deviceSecret>`.
- `ticket_admin_commands` orchestrate cross-device ticket operations initiated by admins on the web, executed locally by the Desktop authority, and acknowledged back.
- `payroll_admin_commands` now expose web payroll recalculation only through `snapshot_requested`; Desktop remains the final authority and auto-pays the closed Friday-Thursday period locally. Manual payroll adjustments are disabled; legacy adjustment tables may remain but are not fed by new snapshots.

