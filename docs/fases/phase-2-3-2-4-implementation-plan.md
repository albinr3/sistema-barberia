# Plan De Implementacion Fase 2.3 Y 2.4

## Objetivo

Implementar el booking autenticado de clientes y la administracion operativa de citas sobre la fundacion ya creada en
Fase 2.0, 2.1 y 2.2.

Fase 2.3 debe permitir que un cliente autenticado reserve, vea y cancele citas usando la disponibilidad cloud existente.
Fase 2.4 debe permitir que admin/owner opere esas citas: filtrar, confirmar, reasignar, cancelar, marcar no-show, revisar
auditoria y preparar la superficie de conflictos para el sync posterior.

## Dependencias Antes De Empezar

- Aplicar las migraciones existentes de Fase 2.0/2.2 contra Supabase local o real.
- Validar la RPC `public.get_available_slots` con datos reales de barberos, servicios, asignaciones y disponibilidad.
- Confirmar que existen usuarios reales por rol: `customer`, `barber`, `admin` y `owner`.
- Agregar o validar tests SQL/RLS minimos para lectura de citas por rol.
- Mantener booking anonimo y depositos online fuera del alcance.

Si esas dependencias no estan listas, 2.3 puede avanzar en UI y acciones mockeadas contra contratos, pero no debe marcarse
cerrada.

## Fase 2.3 - Booking Autenticado

### Alcance Funcional

- Convertir `/app/book` en flujo real:
  - seleccionar servicio activo;
  - seleccionar barbero activo compatible o elegir "any available barber";
  - seleccionar fecha;
  - consultar slots con `public.get_available_slots`;
  - elegir hora;
  - confirmar cita;
  - mostrar estado final y proxima cita.
- Convertir `/app/appointments` en vista real:
  - proximas citas del cliente autenticado;
  - historial basico;
  - cancelacion permitida segun politica;
  - estados visibles: `pending`, `confirmed`, `cancelled`, `completed`, `no_show`.
- Crear una operacion transaccional para crear citas:
  - no insertar desde el cliente directamente en `appointments`;
  - validar usuario autenticado y rol `customer`;
  - validar servicio activo;
  - validar barbero activo y asignado al servicio cuando se elija barbero especifico;
  - recalcular disponibilidad en servidor;
  - prevenir solapamientos contra citas `pending` y `confirmed`;
  - insertar cita con `customer_id = auth.uid()`;
  - registrar `audit_log`.
- Crear una operacion transaccional para cancelar citas de cliente:
  - solo la cita propia;
  - solo estados cancelables;
  - bloquear cancelacion fuera de politica si se define ventana minima;
  - registrar `cancelled_at`, `cancelled_by` o metadata equivalente si hace falta migracion.

### Backend Cloud

- Agregar migracion `phase_2_3_booking.sql` para funciones/RPC y restricciones faltantes:
  - `create_appointment(...)`;
  - `cancel_customer_appointment(...)`;
  - indices/constraints contra solapamientos si se puede expresar con exclusion constraint o validacion transaccional;
  - columnas de auditoria de cancelacion solo si el modelo actual no alcanza.
- Mantener RLS como defensa principal:
  - customer lee solo sus citas;
  - customer no hace `insert/update/delete` directo sobre `appointments`;
  - admin/owner gestiona citas;
  - barber lee citas asignadas.
- Usar `America/New_York` como zona operativa para calculo y presentacion de slots.

### Frontend Web

- Crear componentes orientados al flujo:
  - `BookingStepper`;
  - `ServicePicker`;
  - `BarberPicker`;
  - `DatePicker`;
  - `TimeSlotPicker`;
  - `AppointmentSummary`;
  - `AppointmentList`;
  - `CancelAppointmentDialog`.
- Usar Server Actions para mutaciones y clientes Supabase SSR para lectura server-side.
- Mantener UI mobile-first en cliente, siguiendo `src/web/barberia-web/design.md`.
- Mostrar errores en el mismo flujo, sin sacar al usuario de la reserva:
  - slot tomado mientras confirmaba;
  - servicio/barbero inactivo;
  - sesion expirada;
  - cita no cancelable.

### Criterios De Cierre

- Cliente autenticado puede crear una cita real.
- Una cita creada desaparece de slots disponibles para la misma ventana.
- Cliente ve sus proximas citas e historial.
- Cliente puede cancelar una cita permitida.
- Usuario anonimo no puede reservar ni consultar citas.
- Cliente no puede ver ni cancelar citas de otro cliente.
- Barbero/servicio inactivo no aparece en booking.
- Build, lint, typecheck y tests relevantes pasan.

## Fase 2.4 - Admin Operativo

### Alcance Funcional

- Convertir `/admin/appointments` en gestion real de citas:
  - tabla filtrable por fecha, estado, barbero, servicio y cliente;
  - detalle de cita;
  - confirmar cita;
  - cancelar cita;
  - reasignar barbero;
  - marcar completed;
  - marcar no-show;
  - ver auditoria relacionada.
- Convertir el panel `/admin` en resumen operativo:
  - citas de hoy;
  - proximas citas;
  - citas pendientes;
  - no-shows recientes;
  - conflictos abiertos cuando existan.
- Crear primera vista real de conflictos:
  - listar `sync_conflicts`;
  - filtrar por estado/tipo;
  - mostrar detalle;
  - dejar resolucion manual completa para Fase 2.5 si requiere contrato sync.

### Backend Cloud

- Agregar migracion `phase_2_4_admin_operations.sql` si hacen falta funciones privilegiadas:
  - `admin_confirm_appointment(...)`;
  - `admin_cancel_appointment(...)`;
  - `admin_reassign_appointment(...)`;
  - `admin_mark_no_show(...)`;
  - `admin_complete_appointment(...)`.
- Reasignacion debe recalcular disponibilidad y prevenir solapamientos.
- Cada mutacion admin debe registrar `audit_log` con actor, accion, entidad y metadata minima.
- Mantener las operaciones sensibles detras de `requireAdmin()` y RLS admin/owner.
- No implementar sync bidireccional ni resolver conflictos POS todavia; solo exponer lectura operativa segura.

### Frontend Web

- Crear componentes administrativos densos y escaneables:
  - `AppointmentsTable`;
  - `AppointmentFilters`;
  - `AppointmentDetailsDialog`;
  - `ReassignAppointmentDialog`;
  - `AppointmentStatusBadge`;
  - `AuditTrailPanel`;
  - `ConflictList`;
  - `ConflictDetailsPanel`.
- Mantener el admin como herramienta de trabajo, no landing page.
- Confirmar acciones destructivas con dialogos claros.
- Mostrar validacion dentro del dialogo cuando una accion falle.

### Criterios De Cierre

- Admin/owner puede listar, filtrar y abrir detalle de citas.
- Admin/owner puede confirmar, cancelar, reasignar, completar y marcar no-show.
- Reasignar no permite doble reserva.
- Cada cambio operativo genera auditoria.
- Customer y barber no pueden entrar a `/admin/appointments`.
- La vista de conflictos muestra datos reales de `sync_conflicts` cuando existan.
- Build, lint, typecheck y tests relevantes pasan.

## Orden Recomendado

1. Cerrar validacion Supabase real/local de 2.2: migraciones, datos semilla, RLS y RPC de slots.
2. Implementar funciones transaccionales de 2.3 para crear/cancelar citas.
3. Implementar UI cliente de booking y lista de citas.
4. Agregar tests de solapamiento, RLS y acciones de cliente.
5. Implementar funciones admin de 2.4 para cambios de estado y reasignacion.
6. Implementar `/admin/appointments`, resumen admin y vista inicial de conflictos.
7. Agregar tests admin/RLS, Playwright minimo y validacion responsive.
8. Actualizar `phase-2-current-status.md` con resultados reales y pendientes.

## Riesgos Y Reglas De Decision

- Si `get_available_slots` no coincide con la validacion de `create_appointment`, puede aparecer doble reserva. La misma regla debe vivir en el backend transaccional.
- Si se agregan columnas nuevas a `appointments`, deben tener migracion y documentacion de modelo.
- Si se decide permitir reprogramacion por cliente, tratarlo como extension de 2.3, no como requisito implicito.
- Si se decide que admin puede forzar una cita fuera de disponibilidad, pedir confirmacion antes porque rompe la semantica de disponibilidad.
- Si se necesita resolver conflictos de sync con acciones manuales, moverlo a 2.5 salvo que solo sea lectura.

## Validacion

- `npm run typecheck`
- `npm run lint`
- `npm run build`
- Tests unitarios de helpers/actions.
- Tests SQL/RLS para roles.
- Playwright minimo:
  - cliente reserva cita;
  - cliente cancela cita;
  - admin gestiona cita;
  - rutas protegidas rechazan roles incorrectos.

## Referencias Oficiales

- Next.js App Router y Server Actions: https://nextjs.org/docs/app/getting-started/updating-data
- Supabase Auth SSR con Next.js: https://supabase.com/docs/guides/auth/server-side/nextjs
- Supabase Row Level Security: https://supabase.com/docs/guides/database/postgres/row-level-security
