# Edge Functions

Implemented functions:

- `sync-events`: ingests desktop outbox events with device credentials, materializes POS tickets/payments, receives local catalog snapshots, applies authoritative desktop restore snapshots, updates appointment check-in/no-show/completion, and records sync conflicts. Catalog snapshot items are ignored when their `updated_at` is not newer than the current cloud row, and identical snapshot content is skipped so Supabase does not touch `updated_at` and cause a 60-second echo loop.
- `sync-changes`: returns cloud catalog, appointment, and mapping changes for Windows devices using a cursor.
- `appointment-emails`: processes due `appointment_email_jobs`, renders modern English transactional appointment emails, and sends them through Resend.

Sensitive operations still handled by Postgres RPCs:

- Availability lookup.
- Appointment creation, cancellation, rescheduling, no-show, and admin completion.
- Role and profile checks through RLS plus `is_admin_or_owner()`.

Desktop devices must send:

- `x-device-id`: UUID from `sync_devices`.
- `Authorization: Bearer <deviceSecret>`.

`sync-events` and `sync-changes` must be deployed with JWT verification disabled
(`verify_jwt = false` in `config.toml`) because the bearer token is the device
secret, not a Supabase Auth JWT. Both functions validate the device credentials
against `sync_devices` before processing requests.

`appointment-emails` also runs with JWT verification disabled because it is invoked by `pg_cron`/`pg_net`. It must receive
`Authorization: Bearer <APPOINTMENT_EMAIL_INTERNAL_SECRET>` or `x-internal-secret: <APPOINTMENT_EMAIL_INTERNAL_SECRET>`.

Catalog identity is not assumed. Windows sends local catalog snapshots; admins map local barber/service ids to cloud ids in `/admin/sync`.

Desktop restore events (`desktop.restore_applied`) are authoritative for POS tickets, ticket items, and payments from the sending device. The function upserts rows present in the restored snapshot and marks missing rows with `restore_reverted_at` instead of deleting them, so Web reports can exclude reverted rows while audit history remains available.
