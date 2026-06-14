# Estado Actual Fase 2

## Estado

Fase 2.0 avanzada y validada a nivel de build web local.

Fase 2.1 avanzada para rutas autenticadas y separacion server-side por rol.

Fase 2.2 implementada a nivel de codigo para catalogo y disponibilidad web: `/admin/catalog` ya no es placeholder y
admin/owner puede gestionar barberos, servicios, asignaciones, reglas semanales, excepciones por fecha y preview de slots
contra una RPC de Supabase. Falta aplicar migraciones contra un proyecto Supabase real y validar RLS/RPC con usuarios reales.

Se creo la fundacion web/cloud para login obligatorio, rutas por rol y esquema Supabase base. La implementacion todavia no reemplaza ni modifica la operacion local WinUI de Fase 1.

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
  - `barber_services`
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
  - CRUD basico de barberos, servicios, asignaciones barbero-servicio, reglas semanales y excepciones por fecha.
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
- `tickets` y `ticket_items` cloud quedan diferidos hasta definir el protocolo POS/sync.
- Edge Functions quedan documentadas, no implementadas, hasta cerrar contratos.

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
- Definir contrato sync desktop-cloud antes de tablas POS cloud.
- Implementar booking real en Fase 2.3 usando el contrato de disponibilidad ya definido.
- Implementar administracion operativa de citas en Fase 2.4.
- Agregar Playwright para login, registro, recuperacion de contrasena, proteccion de rutas y redireccion por rol.
