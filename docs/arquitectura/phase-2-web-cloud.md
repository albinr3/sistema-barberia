# Arquitectura Fase 2 Web/Cloud

## Objetivo

Fase 2 agrega una superficie web autenticada para clientes, barberos y administradores, respaldada por Supabase Auth/PostgreSQL/RLS. La app Windows local sigue operando offline-first y no depende de la nube para caja, turnos locales o POS.

## Superficies

- `src/web/barberia-web`: Next.js App Router, TypeScript y Supabase SSR.
- `src/cloud/supabase`: configuracion Supabase, migraciones y contratos futuros de Edge Functions.
- App desktop Fase 1: mantiene SQLite local, outbox y operacion presencial.

## Entrada Y Auth

- `/` muestra login/registro.
- Usuarios anonimos solo acceden a login, registro, recuperacion y verificacion.
- Rutas internas se protegen con `proxy.ts` de Next.js.
- `/admin` y sus subrutas requieren `profiles.role` igual a `admin` u `owner`.
- Las paginas admin usan guard server-side `requireAdmin()` ademas del bloqueo del `proxy`.
- `/app` redirige segun rol:
  - `customer` a portal cliente.
  - `barber` a panel barbero.
  - `admin` y `owner` a admin web.

## Autoridad De Datos

- Supabase manda en:
  - usuarios web y perfiles de dominio;
  - catalogo cloud de barberos y servicios;
  - precios, imagenes, disponibilidad y booking;
  - citas creadas desde web.
- Desktop manda en:
  - tickets walk-in;
  - caja/autocaja;
  - pagos locales en efectivo;
  - operacion local en vivo;
  - hardware POS;
  - reportes locales offline.

## Modelo Inicial

`profiles` usa `auth.users.id` como llave primaria. Supabase Auth conserva la autoridad de credenciales.

Tablas iniciales:

- `profiles`
- `barbers`
- `services`
- `availability_rules`
- `availability_exceptions`
- `appointments`
- `sync_events`
- `sync_conflicts`
- `audit_log`

`tickets` y `ticket_items` no se crean en la primera migracion porque todavia falta definir el contrato de sync POS desde desktop.

## Catalogo Y Disponibilidad

Fase 2.2 convierte `/admin/catalog` en la superficie web principal para administrar catalogo cloud:

- `barbers`: nombre visible, estacion `B-#`, imagen opcional y `is_active`.
- `services`: nombre, descripcion, precio base, duracion, orden e `is_active`.
- `availability_rules`: reglas semanales por barbero en horario local de New Jersey.
- `availability_exceptions`: cierre completo o reemplazo de horario para una fecha especifica.

La RPC `public.get_available_slots(service_id uuid, starts_on date, ends_on date, barber_id uuid default null)` es el contrato
de lectura para preview admin y booking futuro. Devuelve slots en `timestamptz` calculados con zona `America/New_York`,
filtra barberos/servicios inactivos, aplica excepciones por fecha sobre reglas semanales y excluye citas
`pending` o `confirmed` que se solapen.

No hay booking anonimo ni creacion de citas en Fase 2.2. Fase 2.3 debe consumir esta RPC y crear citas mediante una
operacion transaccional separada.

## Seguridad

- RLS se activa desde la primera migracion.
- Clientes pueden leer/actualizar su perfil y sus citas.
- Barberos pueden leer citas asignadas a su perfil.
- Admin/owner pueden administrar catalogo, disponibilidad y citas.
- La UI administrativa no se considera seguridad por si sola: el bloqueo principal ocurre en `proxy.ts` y RLS.
- Clientes no escriben citas directamente contra la tabla; crear, cancelar o reprogramar debe pasar por RPC/Edge Functions transaccionales.
- `audit_log` y sync son superficies de lectura admin; escritura debe venir de funciones privilegiadas.
- Cambios de rol, resolucion de conflictos, sync desktop y operaciones sensibles deben implementarse con RPC/Edge Functions.
- Las mutaciones de catalogo web se ejecutan desde Server Actions protegidas por `requireAdmin()` y RLS admin/owner.
- Desactivar un barbero desde web se bloquea si existen citas futuras `pending` o `confirmed`; esas citas deben resolverse
  en la administracion operativa antes de ocultar al barbero de nuevos flujos.

## Sync

Fase 2.5 convierte el esqueleto inicial en una sincronización operativa bidireccional Desktop-Cloud:

- **Auth de Dispositivos:** Los clientes Windows se autentican contra Supabase usando un `device_id` y `device_secret` registrado en `sync_devices`. No se utilizan sesiones de usuario (`auth.users`) ni `service_role` en la capa Desktop.
- **Push (Desktop a Cloud):** Se utiliza la Edge Function `sync-events` para recibir eventos idempotentes desde la cola (outbox) local de Windows (`ticket.created`, `payment.collected`, etc.).
- **Pull (Cloud a Desktop):** Se utiliza la Edge Function `sync-changes` para servir cambios de catálogo y nuevas citas web de forma incremental usando cursores.
- **Autoridad:** Desktop es la autoridad para la operación presencial, tickets y pagos (`cash`, `zelle`). Cloud es la autoridad para el catálogo futuro y las citas web.

El contrato técnico detallado y las estructuras de eventos (JSON) viven en `docs/arquitectura/phase-2-5-sync-contract.md`. Las tablas cloud (ej. `synced_tickets`, `synced_payments`) almacenan el historial materializado proveniente de Windows.

## Emails Transaccionales

Las citas web generan emails transaccionales para clientes mediante una cola en PostgreSQL (`appointment_email_jobs`) y
la Edge Function `appointment-emails`. La cola se alimenta desde triggers sobre `appointments`, por lo que cubre cambios
hechos por RPC web y por sincronizacion desktop cuando el estado cloud cambia a `no_show` o `completed`.

Los emails son en ingles, usan horario `America/New_York`, cargan el logo publico desde
`${PUBLIC_SITE_URL}/email/master-clips-logo.png` y se envian por Resend. La funcion se invoca con `pg_cron`/`pg_net`
usando un secreto interno; las credenciales reales se configuran como secretos de Supabase y no se guardan en el repo.

## Referencias Oficiales

- Next.js App Router: https://nextjs.org/docs/app
- Supabase Auth SSR para Next.js: https://supabase.com/docs/guides/auth/server-side/nextjs
- Supabase RLS: https://supabase.com/docs/guides/database/postgres/row-level-security
- Supabase Edge Functions: https://supabase.com/docs/guides/functions
