# Supabase Cloud

Fase 2 cloud foundation for Auth, PostgreSQL, RLS, booking and sync contracts.

## Current Scope

- `profiles` extends Supabase Auth users with domain profile data only.
- Catalog and appointment tables are created with RLS enabled from the first migration.
- Phase 2.2 adds catalog/availability indexes and the `public.get_available_slots` RPC for authenticated availability preview and future booking.
- Sync tables are skeletal contracts for later desktop/cloud integration.
- POS ticket cloud tables are intentionally deferred until the desktop event protocol is explicit.

## Local Use

Install the Supabase CLI, then from this folder run migrations against a local or linked Supabase project after project credentials are approved.

Do not expose service-role or secret keys to the web app. Client code must use the publishable key and RLS. Legacy `anon` keys are supported only as a fallback for older Supabase projects.

## Availability Contract

`public.get_available_slots(service_id uuid, starts_on date, ends_on date, barber_id uuid default null)` returns active
bookable slots in `America/New_York`. It filters inactive barbers, inactive services and inactive barber-service
assignments, applies date exceptions over weekly rules, and removes slots that overlap `pending` or `confirmed`
appointments.
