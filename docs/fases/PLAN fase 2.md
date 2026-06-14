# Plan Fase 2: Web Login + Booking + Backend Cloud

## Resumen

Crear Fase 2 como una web moderna con **Next.js + TypeScript + Supabase**.
La **pagina principal debe ser login/registro de usuario**. Despues de iniciar sesion, el usuario vera las funciones que le correspondan: booking, cuenta de cliente, panel de barbero o panel admin web.

Backend y datos viviran en **Supabase/PostgreSQL/Auth/RLS/Storage/Edge Functions**. La app Windows local seguira siendo autoridad para operacion en vivo, tickets, caja y POS; la web/Supabase sera autoridad principal para usuarios, catalogo, booking, disponibilidad y administracion remota.

Agentes asignados:
- **Alfredo**: frontend web, experiencia de login, booking autenticado, cuenta cliente, admin web, accesibilidad y pruebas E2E.
- **Julio**: backend cloud, Supabase schema, Auth/RLS, Edge Functions, sync, conflictos y seguridad.

Nota operativa: cualquier decision fuerte de frontend web debe validarse con Alfredo, y cualquier decision fuerte de backend cloud/API debe validarse con Julio antes de implementarla.

## Principio De Entrada

- La ruta principal `/` muestra login/registro, no booking publico.
- El usuario debe autenticarse antes de reservar, ver citas, administrar barberos o acceder a paneles internos.
- Despues del login, la app redirige segun rol:
  - `customer`: portal de cliente con reservar cita, proximas citas, historial y perfil.
  - `barber`: panel de barbero con agenda asignada y estado de citas.
  - `admin` / `owner`: panel admin web con citas, barberos, servicios, disponibilidad, sync y reportes.
- Usuarios no autenticados solo pueden ver pantallas necesarias para autenticacion: login, registro, recuperacion de contrasena y verificacion.
- Si mas adelante se quiere una landing publica o booking sin login, debe tratarse como cambio de alcance y validarse antes.

## Cambios Clave

- Crear estructura nueva:
  - `src/web/barberia-web`: app Next.js con App Router.
  - `src/cloud/supabase`: migraciones, Edge Functions, politicas RLS y documentacion backend.
  - `docs/fases/phase-2-current-status.md`: estado operativo de Fase 2.
  - `docs/arquitectura/phase-2-web-cloud.md`: arquitectura web/cloud, auth, roles, sync y autoridad de datos.

- Backend minimo:
  - Tablas: `profiles`, `barbers`, `services`, `barber_services`, `availability_rules`, `availability_exceptions`, `appointments`, `tickets`, `ticket_items`, `sync_events`, `sync_conflicts`, `audit_log`.
  - Roles: `customer`, `barber`, `admin`, `owner`.
  - RLS activado en tablas expuestas.
  - Supabase Auth como puerta de entrada obligatoria.
  - Edge Functions/RPC para disponibilidad, crear/cancelar citas, sync desktop y operaciones sensibles.
  - `barbers.is_active=false` oculta al barbero de booking y nuevos flujos sin borrar historial.
  - Estaciones activas usan `station_code` tipo `B-1` y deben ser unicas entre barberos activos.

- Frontend moderno:
  - Primera pantalla: login/registro de usuario.
  - Portal de cliente autenticado: reservar cita, ver proximas citas, historial, cancelacion/reprogramacion segun reglas y perfil.
  - Booking autenticado: servicio -> barbero opcional -> fecha/hora -> confirmacion.
  - Admin web autenticado: dashboard, citas, barberos, servicios, disponibilidad, historial sincronizado, conflictos y estado de sync.
  - Panel de barbero autenticado: agenda, citas del dia y estado operativo cuando aplique.
  - UI sobria y moderna: mobile-first para cliente; admin denso, escaneable y eficiente.
  - Componentes base: `AuthShell`, `LoginForm`, `RegisterForm`, `RoleRedirect`, `BookingStepper`, `ServicePicker`, `BarberPicker`, `CalendarAvailability`, `AppointmentSummary`, `DataTable`, `StatusBadge`, `FormDialog`, `SyncStatusIndicator`.

## Interfaces Y Reglas

- Stack confirmado: **Next.js + Supabase**.
- Login obligatorio para entrar al producto web.
- Catalogo confirmado: **Web/Supabase manda** para barberos, servicios, precios, imagenes, disponibilidad, `is_active` y datos usados por booking.
- Desktop conserva autoridad para:
  - tickets walk-in;
  - caja/autocaja;
  - pagos locales en efectivo;
  - operacion local en vivo;
  - hardware POS;
  - reportes locales offline.

- APIs/funciones principales:
  - `GET availability`: devuelve slots disponibles por servicio, barbero opcional y rango de fechas para usuarios autenticados.
  - `POST appointments`: crea cita autenticada con validacion transaccional y prevencion de solapamiento.
  - `POST appointments/:id/cancel`: cancela segun politica.
  - `GET me/appointments`: historial del cliente autenticado.
  - `POST sync/events`: recibe eventos idempotentes desde el outbox local.
  - `GET sync/changes?since=cursor`: entrega cambios cloud relevantes a Windows.

- Rutas web esperadas:
  - `/`: login/registro.
  - `/auth/reset-password`: recuperacion de contrasena.
  - `/app`: redireccion segun rol.
  - `/app/book`: booking autenticado para clientes.
  - `/app/appointments`: citas del usuario.
  - `/app/profile`: perfil del usuario.
  - `/barber`: panel de barbero.
  - `/admin`: panel admin/owner.

- Conflictos:
  - Tickets/POS: gana desktop.
  - Catalogo: gana web/Supabase.
  - Citas: Supabase previene doble reserva en cloud; si Windows estuvo offline y hay choque operativo, registrar `sync_conflicts` para resolucion admin.
  - Desactivar barbero con citas futuras confirmadas: bloquear hasta reasignar o cancelar esas citas.

- Depositos:
  - No entran en el primer MVP.
  - Quedan como subfase posterior con Stripe/webhooks, `appointment_holds`, `payments`, politica de reembolso/no-show y confirmacion solo por webhook verificado.

## Entregas

1. **Fase 2.0 - Fundacion Auth/Web/Cloud**
   - Crear proyectos web/cloud, Supabase local, migraciones base, Auth/RLS, roles, layout de autenticacion, docs y decision-log.
   - Validar con Alfredo la experiencia de login/registro y con Julio el modelo de Auth/RLS.

2. **Fase 2.1 - Portal autenticado y roles**
   - Implementar `RoleRedirect`, proteccion de rutas, portal cliente, panel barbero base y shell admin.
   - Asegurar que ninguna ruta interna sea visible sin sesion valida.

3. **Fase 2.2 - Catalogo y disponibilidad**
   - Admin web para barberos/servicios/disponibilidad.
   - API de disponibilidad.
   - Filtros centrales para `is_active`.

4. **Fase 2.3 - Booking autenticado**
   - Booking completo detras del login, sin depositos.
   - Confirmacion/cancelacion de citas.
   - Prevencion de solapamientos.

5. **Fase 2.4 - Admin operativo**
   - Gestion de citas.
   - Reasignacion/cancelacion/no-show.
   - Auditoria y vista de conflictos.

6. **Fase 2.5 - Sync con Windows**
   - Desktop -> Supabase: tickets, pagos, historial y lifecycle timestamps.
   - Supabase -> Desktop: citas futuras, catalogo y disponibilidad.
   - Errores y conflictos visibles en admin.

7. **Fase 2.6 - Reportes cloud**
   - Reportes web sobre citas, tickets sincronizados, servicios, barberos y sync.

8. **Fase 2.7 - Depositos online**
   - Stripe, holds, pagos, webhooks, reembolsos y reglas de no-show.

## Test Plan

- Frontend:
  - Unit tests con Vitest/Testing Library.
  - Formularios con React Hook Form + Zod.
  - Playwright para login, registro, recuperacion de contrasena, proteccion de rutas, redireccion por rol, booking autenticado, admin CRUD, ocultar inactivos y responsive.
  - Axe/accessibility smoke tests.

- Backend:
  - Tests SQL/RLS por rol.
  - Tests de Auth/RLS para impedir acceso anonimo a datos internos.
  - Tests de disponibilidad, excepciones y no solapamiento.
  - Tests de sync idempotente y reintentos.
  - Tests de conflictos offline.
  - Tests de seguridad para PII, roles y Edge Functions.

- Validacion final:
  - `/` carga login/registro como primera pantalla.
  - Un usuario sin sesion no puede entrar a booking, cuenta, admin ni panel barbero.
  - Login de cliente redirige al portal de cliente.
  - Login de admin/owner redirige al admin web.
  - Login de barbero redirige al panel de barbero.
  - Booking autenticado crea cita visible en admin.
  - Cita confirmada bloquea al barbero 15 minutos antes.
  - No-show respeta ventana de 10 minutos.
  - Barbero/servicio inactivo no aparece en booking.
  - Citas futuras bloquean desactivacion hasta resolverlas.
  - Desktop puede seguir operando offline sin depender de Supabase.

## Supuestos

- Idioma inicial: ingles, siguiendo la UI actual del desktop, salvo que se confirme espanol/bilingue.
- Zona operativa: New Jersey/Eastern Time.
- Supabase Auth gestiona contrasenas; la base de negocio no guarda contrasenas.
- Imagenes de barberos van en Supabase Storage y desktop podra cachearlas luego.
- No se implementan pagos online en el primer MVP de Fase 2.
- No habra booking anonimo en Fase 2 MVP.
- Referencias oficiales usadas para el stack: [Next.js App Router](https://nextjs.org/docs/app), [Supabase Auth](https://supabase.com/docs/guides/auth), [Supabase RLS](https://supabase.com/docs/guides/database/postgres/row-level-security), [Supabase Edge Functions](https://supabase.com/docs/guides/functions).
