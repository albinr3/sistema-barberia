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
      "price": 25.00
    }
  ]
}
```

#### `payment.collected`
```json
{
  "ticket_id": "uuid-del-ticket",
  "payment_method": "cash | zelle",
  "amount": 25.00
}
```

## 4. Cloud a Desktop (Pull Changes)

**Endpoint:** `POST /functions/v1/sync-changes`

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
      { "type": "ticket.reassign", "data": { "id": "...", "local_ticket_id": "...", "target_barber_id": "..." } }
    ]
  }
}
```

### Acuse de Recibo (Ack)
Una vez que Desktop procesa un comando de la lista `ticket_commands`, emite un evento normal hacia `POST /functions/v1/sync-events` con `event_type` = `ticket_admin_command.applied` o `ticket_admin_command.failed` y el `command_id` en el `payload`.

## 5. Resolución de Conflictos

Si hay incongruencias (e.g. Desktop envía un pago para un ticket que no existe en Cloud por un evento perdido o corrupto), se registra en la tabla `sync_conflicts`. El dashboard web `/admin/sync` permitirá revisar estos casos. En esta fase, no permitiremos que la nube sobreescriba tickets ya cobrados localmente.

## 6. Tickets Dashboard Web

La ruta web `/tickets-dashboard` es una pantalla de sala read-only protegida para `admin`/`owner`. Consume `synced_tickets` materializados desde Desktop y no ejecuta acciones operativas sobre tickets. Para copiar el display local, `synced_tickets` conserva `display_ticket_number`, `ticket_date` y `checked_in_at`; los eventos antiguos que no incluyan esos campos no deben borrar valores ya materializados.

La pantalla excluye tickets asociados a `appointment_id` como filas normales de espera/llamado, porque esos turnos se usan como registros operativos para Barber Panel/Cash Box. Desktop sigue siendo autoridad para cola, POS, pagos y cambios de estado.
