# Edge Functions

Implemented functions:

- `sync-events`: ingests desktop outbox events with device credentials, materializes POS tickets/payments, receives local catalog snapshots, updates appointment check-in/no-show/completion, and records sync conflicts.
- `sync-changes`: returns cloud catalog, appointment, and mapping changes for Windows devices using a cursor.

Sensitive operations still handled by Postgres RPCs:

- Availability lookup.
- Appointment creation, cancellation, rescheduling, no-show, and admin completion.
- Role and profile checks through RLS plus `is_admin_or_owner()`.

Desktop devices must send:

- `x-device-id`: UUID from `sync_devices`.
- `Authorization: Bearer <deviceSecret>`.

Catalog identity is not assumed. Windows sends local catalog snapshots; admins map local barber/service ids to cloud ids in `/admin/sync`.
