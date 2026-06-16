# Contrato de Sincronización Fase 2.5 (Desktop ↔ Cloud)

Este documento define la estructura de datos, los flujos y las responsabilidades para la sincronización entre la aplicación Desktop (Windows) y Supabase (Cloud) para la Fase 2.5.

## 1. Principios Generales

- **Idempotencia:** Todo evento enviado a la nube tiene un `source_event_id` único por dispositivo. Si se retransmite, Supabase lo reconoce y no duplica las acciones.
- **Autoridad:**
  - **Desktop manda en la operación local:** Tickets, turnos (walk-in), y pagos (`cash`, `zelle`).
  - **Cloud manda en catálogo y futuro:** Servicios, barberos, disponibilidad, citas web futuras.
- **Autenticación por dispositivo:** La capa Desktop no utiliza sesión de usuario web (`service_role` ni JWT de Auth). Se autentica contra las Edge Functions usando un `device_id` y un `device_secret` registrados.

## 2. Autenticación y Cabeceras

Todas las peticiones del Desktop a las Edge Functions deben incluir:
```http
Authorization: Bearer <device_secret>
x-device-id: <device_id>
```

Las funciones `sync-events` y `sync-changes` se despliegan con `verify_jwt = false`
en Supabase porque `Authorization` contiene el secreto del dispositivo, no un JWT
de Supabase Auth. La validacion real ocurre dentro de cada funcion contra
`sync_devices`.

## 3. Desktop a Cloud (Push Events)

**Endpoint:** `POST /functions/v1/sync-events`

El Desktop agrupa eventos en un arreglo y los envía. Cada evento representa un cambio inmutable de la operación local.

### Estructura de Evento Base (Envelope)

```json
{
  "events": [
    {
      "source_event_id": "uuid-unico-del-evento-local",
      "schema_version": "1.0",
      "occurred_at": "2026-06-13T15:30:00Z",
      "event_type": "ticket.created",
      "aggregate_type": "ticket",
      "aggregate_id": "uuid-del-ticket",
      "payload": { ... }
    }
  ]
}
```

### Tipos de Eventos (Payloads)

#### `ticket.created`
```json
{
  "display_ticket_number": 12,
  "ticket_date": "2026-06-15",
  "customer_name": "Juan Perez",
  "assigned_barber_id": "uuid-del-barbero-opcional",
  "checked_in_at": "2026-06-15T15:30:00Z",
  "status": "waiting"
}
```

#### `ticket.called` / `ticket.started` / `ticket.completed` / `ticket.cancelled`
```json
{
  "display_ticket_number": 12,
  "ticket_date": "2026-06-15",
  "customer_name": "Juan Perez",
  "assigned_barber_id": "uuid-del-barbero-opcional",
  "status": "in_progress | completed | cancelled",
  "barber_id": "uuid-del-barbero",
  "checked_in_at": "2026-06-15T15:30:00Z",
  "items": [
    {
      "service_id": "uuid-del-servicio",
Desktop consulta cambios periódicamente, enviando el cursor (timestamp del último evento sincronizado).

### Petición
```json
{
  "cursor": "2026-06-12T00:00:00Z"
}
```

### Respuesta
```json
{
  "new_cursor": "2026-06-13T16:00:00Z",
  "changes": {
    "catalog": [
      { "type": "upsert_service", "data": { "id": "...", "name": "Corte", "price": 20 } },
      { "type": "upsert_barber", "data": { "id": "...", "name": "Albin", "is_active": true } }
    ],
    "appointments": [
      { "type": "upsert_appointment", "data": { "id": "...", "start_time": "..." } },
      { "type": "cancel_appointment", "data": { "id": "..." } }
    ],
    "ticket_commands": [
      { "type": "ticket.reassign", "data": { "id": "...", "local_ticket_id": "...", "target_barber_id": "..." } },
      { "type": "ticket.cancel", "data": { "id": "...", "local_ticket_id": "..." } }
    ]
  }
}
```

### Acuse de Recibo (Ack)
Una vez que Desktop procesa un comando de la lista `ticket_commands`, emite un evento normal hacia `POST /functions/v1/sync-events` con `event_type` = `ticket_admin_command.applied` o `ticket_admin_command.failed` y el `command_id` en el `payload`.

### Payroll Web Con Autoridad Desktop

La ruta `/admin/payroll` permite operar nomina desde web, pero no convierte a Supabase en autoridad de pago. Web crea comandos en `payroll_admin_commands`; Desktop los descarga en `sync-changes`, recalcula contra SQLite local y confirma con eventos hacia `sync-events`.

`sync-changes` incluye:

```json
{
  "changes": {
    "payroll_commands": [
      {
        "type": "payroll.snapshot_requested | payroll.adjustment_added | payroll.pay_requested",
        "data": {
          "id": "uuid-del-comando",
          "source_device_id": "uuid-del-dispositivo",
          "start_date": "2026-06-05",
          "end_date": "2026-06-12",
          "payload": {}
        }
      }
    ]
  }
}
```

Desktop responde con:

- `payroll.snapshot`: materializa `synced_payroll_periods`, `synced_payroll_lines` y `synced_payroll_adjustments`.
- `payroll_admin_command.applied`: marca el comando web como aplicado.
- `payroll_admin_command.failed`: marca el comando web como fallido y conserva `error_message`.
- `desktop.sync_heartbeat`: actualiza `sync_devices.last_sync_at` y `pending_outbox_count`.

Reglas de seguridad para `payroll.pay_requested`:

- Web solo crea el comando si `last_sync_at` esta fresco.
- Web bloquea el pago si `pending_outbox_count` es mayor que cero.
- Web bloquea el pago si ya existe un comando `pending` para el mismo periodo.
- Web bloquea el pago si el periodo viernes-jueves aun no ha cerrado en `America/New_York`.
- Desktop vuelve a validar: si hay eventos locales pendientes, si el periodo esta abierto o si ya fue pagado, responde `failed`.
- Web nunca muestra `Paid` por optimismo; espera `payroll_admin_command.applied` y un `payroll.snapshot` con `state = "paid"`.

## 5. Resolución de Conflictos

Si hay incongruencias (e.g. Desktop envía un pago para un ticket que no existe en Cloud por un evento perdido o corrupto), se registra en la tabla `sync_conflicts`. El dashboard web `/admin/sync` permitirá revisar estos casos. En esta fase, no permitiremos que la nube sobreescriba tickets ya cobrados localmente.

## 6. Tickets Dashboard Web

La ruta web `/tickets-dashboard` es una pantalla de sala read-only protegida para `admin`/`owner`. Consume `synced_tickets` materializados desde Desktop y no ejecuta acciones operativas sobre tickets. Para copiar el display local, `synced_tickets` conserva `display_ticket_number`, `ticket_date` y `checked_in_at`; los eventos antiguos que no incluyan esos campos no deben borrar valores ya materializados.

La pantalla excluye tickets asociados a `appointment_id` como filas normales de espera/llamado, porque esos turnos se usan como registros operativos para Barber Panel/Cash Box. Desktop sigue siendo autoridad para cola, POS, pagos y cambios de estado.

## 7. Ticket History Web

La ruta `/admin/ticket-history` es un monitor read-only y paginado para consultar el historial de tickets del día o de días anteriores, emulando la página homónima del Desktop. Cuenta con filtros por fecha, barbero, estatus, y número de ticket. Muestra el modal detallado del servicio, recibo, referencia de pago, timeline y estatus de cobro.

## 8. Ticket Dashboard Desktop Y Comandos Web

El modulo Desktop `Ticket Dashboard` (`PublicDisplayPage`) no se suscribe directamente a Supabase. La ruta esperada para una reasignacion o cancelacion hecha desde Web es:

1. Web crea un comando pendiente en `ticket_admin_commands`.
2. `DesktopSyncService` descarga comandos en su proximo ciclo de `sync-changes`.
3. Desktop aplica el comando con `LocalAdminService.ReassignTurn` o `LocalAdminService.CancelTurn`, actualiza SQLite y encola el estado final mas el ack del comando.
4. `PublicDisplayPage` vuelve a leer SQLite local y redibuja la pantalla.

La configuracion recomendada para operacion visible es `pollSeconds: 30` en `%LocalAppData%\BarberiaSystem\config\sync-settings.json`; `PublicDisplayPage` refresca su snapshot local cada 5 segundos mientras esta cargada. Con esa cadencia, una operacion web debe verse en el Ticket Dashboard Desktop sin salir y entrar de la pagina, normalmente dentro de 30 a 60 segundos. No se usa Realtime ni sockets en esta fase para preservar el modelo offline-first.

## 8. Web Alerts y Active Queue Monitor

El dashboard web en `/admin` muestra **Alertas Operativas** (Web Alerts) calculadas desde el estado de los tickets sincronizados. Esto sigue las reglas del Desktop: un ticket en `waiting` genera alerta a los 30 minutos, y un ticket en `called` genera alerta a los 4 minutos de no iniciar el servicio.
La página `/admin/tickets` (Active Queue Monitor) despliega una tabla con todos los tickets en `waiting`, `called` o `in_progress`, permitiendo **Reasignar** o **Cancelar** tickets directamente desde la nube, enviando estos comandos hacia Desktop usando el mecanismo descrito en la sección 7.
