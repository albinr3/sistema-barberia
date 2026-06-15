# Estado Actual Fase 2

## Estado

Fase 2.0 avanzada y validada a nivel de build web local.

Fase 2.1 avanzada para rutas autenticadas y separacion server-side por rol.

Fase 2.2 implementada a nivel de codigo para catalogo y disponibilidad web: `/admin/catalog` ya no es placeholder y
admin/owner puede gestionar barberos, servicios, reglas semanales, excepciones por fecha y preview de slots
contra una RPC de Supabase.

Fase 2.3 implementada a nivel de código con flujos de reservas para clientes (rutas `/app/book` y `/app/appointments`).

Fase 2.4 implementada a nivel de código con panel operativo para administrador (`/admin/appointments` y dashboard) y vista para barbero (`/barber`).

Fase 2.5 implementada a nivel de código con sincronización bidireccional entre Windows y Supabase, a través de Edge Functions protegidas con secreto de dispositivo y outbox de eventos.

Extension de citas QR/reprogramacion implementada a nivel de codigo:
- `/admin/appointments` puede reprogramar citas futuras `pending` o `confirmed` manteniendo el mismo barbero y servicio.
- Las citas tienen `appointment_code` estable, QR visible para cliente/admin y campos de check-in, completado y no-show.
- Desktop puede importar citas via mappings, escanear el QR en Barber Panel, cerrar el cobro en Cash Box y completar la cita en cloud despues del cobro.
- Desktop marca `no_show` automaticamente si pasan 10 minutos despues de la hora sin check-in.

Se creo la fundacion web/cloud para login obligatorio, rutas por rol y esquema Supabase base. La implementacion no reemplaza la operacion local WinUI de Fase 1, pero ya agrega sincronizacion opcional, QR de cita y cierre cloud desde Desktop cuando `sync-settings.json` esta configurado.

## Incluido

- Web scaffold en `src/web/barberia-web`.
- Next.js App Router + TypeScript + Supabase SSR.
- `/` como pantalla inicial de login/registro.
- Recuperacion de contrasena con Supabase Auth:
  - `/auth/reset-password` solicita el enlace por email.
  - `/auth/confirm?next=/auth/update-password` confirma el token de Supabase.
  - `/auth/update-password` permite guardar la nueva contrasena.
- `proxy.ts` de Next.js para bloquear rutas internas sin sesion y restringir `/admin` a `admin`/`owner`.
- `proxy.ts` tambien separa rutas de cliente y barbero por rol:
  - `/app` y subrutas requieren `customer`.
  - `/barber` y subrutas requieren `barber`.
  - `/admin` y subrutas requieren `admin`/`owner`.
- Guards server-side por rol en rutas cliente, barbero y admin.
- Rutas iniciales separadas:
  - `/app` y rutas cliente.
  - `/barber` y rutas de barbero.
  - `/admin` y rutas admin/owner.
- Baseline visual web alineada con `src/web/barberia-web/design.md` para login, shell de app, botones, badges, formularios y paneles placeholder.
- Supabase scaffold en `src/cloud/supabase`.
- Migracion base para:
  - `profiles`
  - `barbers`
  - `services`
  - `availability_rules`
  - `availability_exceptions`
  - `appointments`
  - `sync_events`
  - `sync_conflicts`
  - `audit_log`
- RLS activado desde la primera migracion.
- Bootstrap SQL para promover el primer usuario admin/owner despues de crearlo en Supabase Auth.
- Scripts web validados localmente:
  - `npm run typecheck`
  - `npm run lint`
  - `npm run build`
- Test unitario inicial de rutas/roles con Vitest.
- Migracion incremental `202606130002_phase_2_2_catalog_availability.sql` con indices de catalogo/disponibilidad y RPC
  `get_available_slots`.
- `/admin/catalog` operativo para:
  - CRUD basico de barberos, servicios, reglas semanales y excepciones por fecha.
  - bloqueo server-side al intentar desactivar un barbero con citas futuras `pending` o `confirmed`.
  - preview de slots disponibles basado en la RPC.
- Helpers compartidos para filtros activos y formato de precios.
- Test unitario de helpers de catalogo.

## Decisiones Aplicadas

- Login/registro vive en `/`; no hay booking anonimo.
- Las rutas por rol se mantienen separadas desde el scaffold.
- Las funciones administrativas solo son visibles para perfiles `admin` u `owner`.
- Las rutas cliente son solo para `customer`; las rutas de barbero son solo para `barber`.
- `profiles` extiende Supabase Auth, pero no duplica credenciales.
- Web/Supabase manda sobre catalogo, booking y disponibilidad.
- Desktop mantiene autoridad para operacion local, caja, POS y offline.
- `synced_tickets` y pagos cloud se alimentan desde el outbox Desktop y pueden asociarse a `appointment_id`.
- Edge Functions `sync-events` y `sync-changes` estan implementadas a nivel de codigo para push/pull, mappings, conflictos y eventos de cita.

## Pendiente Para Cerrar 2.0/2.1

- Crear o vincular proyecto Supabase real.
- Confirmar llaves y variables `.env.local`.
- Probar migraciones con Supabase CLI.
- Crear el primer usuario en Supabase Auth y promoverlo con `src/cloud/supabase/bootstrap/promote-admin.sql`.
- Ejecutar pruebas de rutas/auth contra perfiles reales `customer`, `barber`, `admin` y `owner`.
- Agregar tests SQL/RLS para confirmar permisos reales por rol.

## Pendiente Para 2.2+

- Aplicar migraciones de Supabase en entorno real/local y validar `get_available_slots` con datos reales.
- Agregar tests SQL/RLS automatizados para admin/customer/barber.
- Agregar Playwright para login, registro, recuperacion de contrasena, proteccion de rutas y redireccion por rol.
- Validar en entorno real la resolucion de conflictos manual en `/admin/sync`, incluyendo mappings faltantes de catalogo local-cloud.

## Plan Operativo 2.3/2.4

El plan detallado para implementar booking autenticado y administracion operativa de citas ha sido implementado, quedando registradas las decisiones en `docs/fases/phase-2-3-2-4-implementation-plan.md`.

## Plan Operativo 2.5

Extension de Tickets Dashboard web implementada a nivel de codigo:
- `/tickets-dashboard` muestra una pantalla read-only de sala para `admin`/`owner`, inspirada en `PublicDisplayPage` de Desktop.
- La pantalla usa datos sincronizados desde `synced_tickets`, refresca cada 30 segundos y separa `Now Calling`, `Waiting List` y `Barber Status`.
- La sincronizacion de tickets ahora materializa `ticket.called`, `display_ticket_number`, `ticket_date` y `checked_in_at` para conservar el numero visible del dashboard.
- Los tickets asociados a `appointment_id` no se muestran como filas normales del dashboard publico.

La Fase 2.5 de sincronización Windows-Supabase ha sido implementada a nivel de código, incluyendo el contrato técnico en `docs/arquitectura/phase-2-5-sync-contract.md`, tablas POS cloud en la base de datos (e.g. `synced_tickets`, `synced_payments`), Edge Functions (`sync-events`, `sync-changes`) y el dashboard web `/admin/sync`. También se modificó el Desktop para enviar los eventos a través de un Outbox hacia la nube.
