# Plan De Implementacion Fase 2.5

## Objetivo

Implementar la sincronizacion controlada entre la app Windows local y Supabase sin romper la operacion offline-first de
Fase 1.

Fase 2.5 debe permitir:

- Desktop -> Supabase: publicar historial de tickets, pagos locales, eventos operativos y tiempos de ciclo de vida.
- Supabase -> Desktop: descargar citas futuras, catalogo web, disponibilidad y cambios administrativos necesarios para la operacion local.
- Admin web: ver errores, reintentos y conflictos reales en `/admin/sync`.

La regla principal no cambia: Windows sigue operando aunque Supabase este caido o no haya internet.

## Estado Base

Ya existe:

- Outbox SQLite local en `sync_outbox_events`.
- Librerias `Barberia.Sync` y `Barberia.ApiClient` con contratos futuros, sin HTTP real.
- Tablas cloud `sync_events` y `sync_conflicts`.
- Booking, catalogo y citas cloud a nivel de codigo en Fase 2.2, 2.3 y 2.4.
- Vista admin inicial de sync/conflictos.

Todavia falta:

- Crear tablas cloud definitivas para tickets y pagos sincronizados.
- Definir eventos versionados entre desktop y cloud.
- Implementar endpoint seguro para recibir eventos desktop.
- Implementar pull incremental de cambios cloud hacia SQLite local.
- Mapear IDs locales y cloud sin duplicar datos.
- Resolver conflictos de catalogo, servicios, barberos, citas y tickets.

## Principios

- Offline-first: ningun flujo local de kiosco, barbero, pantalla publica, autocaja, reportes locales o nomina debe bloquearse por sync.
- Idempotencia obligatoria: reintentar el mismo evento no puede duplicar tickets, pagos, auditoria ni cambios de catalogo.
- Contratos versionados: todo payload debe incluir `schema_version`.
- Autoridad explicita:
  - Desktop manda para tickets walk-in, pagos locales, caja, tiempos reales de atencion y operacion en vivo.
  - Supabase manda para usuarios web, catalogo cloud, disponibilidad, booking y citas web.
  - Conflictos se registran cuando una regla de autoridad no pueda decidir automaticamente.
- PII minima: sync debe enviar solo los datos necesarios para operacion, historial y reportes.
- Sin secretos en cliente web: credenciales privilegiadas solo viven en Supabase secrets o configuracion protegida del desktop.

## Entregas

### 2.5.0 - Contrato Sync

- Crear documento tecnico `docs/arquitectura/phase-2-5-sync-contract.md`.
- Definir envelopes:
  - `event_id`
  - `schema_version`
  - `source`
  - `source_device_id`
  - `occurred_at`
  - `aggregate_type`
  - `aggregate_id`
  - `event_type`
  - `payload`
  - `idempotency_key`
- Definir cursores de pull:
  - `catalog_cursor`
  - `appointments_cursor`
  - `availability_cursor`
  - `sync_cursor`
- Definir estrategia de identidad:
  - IDs locales se conservan como `local_id`.
  - IDs cloud se conservan como `cloud_id`.
  - Tablas puente evitan depender de nombres o estaciones como llaves.
- Definir eventos minimos:
  - `ticket.created`
  - `ticket.called`
  - `ticket.started`
  - `ticket.completed`
  - `ticket.cancelled`
  - `payment.recorded`
  - `barber.local_status_changed`
  - `catalog.snapshot_requested`
  - `desktop.sync_heartbeat`

No empezar migraciones ni HTTP real hasta cerrar este contrato.

### 2.5.1 - Modelo Cloud Para POS/Historial

- Agregar migracion Supabase `phase_2_5_sync_pos.sql`.
- Crear tablas cloud para datos sincronizados desde desktop:
  - `desktop_devices`
  - `synced_tickets`
  - `synced_ticket_events`
  - `synced_payments`
  - `synced_catalog_mappings`
  - `cloud_change_log`
- Mantener `sync_events` como bandeja de recepcion idempotente.
- Mantener `sync_conflicts` como registro visible y resoluble desde admin.
- Agregar indices por:
  - `source_device_id`
  - `local_id`
  - `occurred_at`
  - `updated_at`
  - `sync_event_id`
- RLS:
  - Admin/owner puede leer historial sync y conflictos.
  - Escritura directa desde usuarios autenticados queda bloqueada.
  - Ingesta escribe mediante funcion privilegiada o Edge Function.

### 2.5.2 - Desktop Push

- Implementar cliente HTTP real en `Barberia.ApiClient` para enviar lotes al endpoint cloud.
- Mantener `UnavailableCloudSyncClient` como fallback cuando no haya configuracion.
- Configurar:
  - `SupabaseUrl`
  - endpoint de sync
  - token o secreto por dispositivo
  - `DeviceId`
  - tamano de lote
  - timeout
- Reusar `SyncOutboxDispatcher` para:
  - enviar lotes pendientes;
  - marcar eventos sincronizados solo despues de respuesta exitosa;
  - registrar error y reintento cuando falle;
  - no bloquear UI ni transacciones locales.
- Encolar eventos desde puntos reales:
  - creacion de ticket;
  - llamada/asignacion;
  - inicio de servicio;
  - cierre en autocaja;
  - cancelacion;
  - pago registrado.

### 2.5.3 - Cloud Ingest

- Crear Edge Function o RPC segura `sync/events`.
- Validar autenticacion de dispositivo antes de procesar.
- Validar envelope y `schema_version`.
- Insertar primero en `sync_events` con llave unica `(source, source_event_id)`.
- Procesar idempotentemente:
  - si el evento ya existe y fue procesado, devolver exito idempotente;
  - si existe fallido, permitir reproceso controlado;
  - si el payload es invalido, marcar `failed` con error visible.
- Materializar datos en tablas `synced_*`.
- Registrar `audit_log` para eventos de alto impacto.
- Crear `sync_conflicts` cuando:
  - un pago referencia un ticket inexistente;
  - un ticket apunta a barbero/servicio sin mapping;
  - un evento llega fuera de orden y no puede aplicarse;
  - un cambio local contradice una cita cloud futura.

### 2.5.4 - Cloud Pull Hacia Desktop

- Crear endpoint `sync/changes`.
- Desktop envia cursores y recibe cambios incrementales.
- Descargar hacia SQLite local:
  - citas futuras `pending` y `confirmed`;
  - cancelaciones/no-show/completed relevantes para desktop;
  - catalogo de servicios activo;
  - barberos activos/inactivos;
  - asignaciones barbero-servicio;
  - disponibilidad necesaria para proteger citas.
- Guardar citas web en el modelo local de reservaciones existente, no en la cola de tickets.
- Aplicar catalogo web con reglas de seguridad:
  - no borrar historial local;
  - no romper pagos historicos;
  - conservar mappings;
  - si hay edicion local concurrente no resuelta, registrar conflicto.

### 2.5.5 - Admin Sync

- Convertir `/admin/sync` en consola real:
  - estado por dispositivo;
  - ultimo heartbeat;
  - eventos recibidos/procesados/fallidos;
  - conflictos abiertos;
  - detalle de payload resumido;
  - acciones de reintento o marcar ignorado cuando aplique.
- Agregar filtros por:
  - dispositivo;
  - rango de fechas;
  - tipo de evento;
  - estado;
  - tipo de conflicto.
- Resolver conflictos solo con acciones explicitas:
  - `resolve_cloud_wins`
  - `resolve_desktop_wins`
  - `map_entity`
  - `ignore_with_note`
- Toda resolucion debe escribir `audit_log`.

### 2.5.6 - Observabilidad Y Operacion

- Agregar logs de sync en desktop sin exponer secretos.
- Mostrar estado simple en admin local si ya existe superficie adecuada:
  - online/offline;
  - eventos pendientes;
  - ultimo sync exitoso;
  - ultimo error.
- Documentar rotacion de token/dispositivo.
- Documentar recuperacion:
  - reprocesar evento;
  - limpiar conflicto;
  - registrar nuevo dispositivo;
  - reconstruir reportes cloud desde `synced_ticket_events`.

## Orden Recomendado

1. Escribir y aprobar `phase-2-5-sync-contract.md`.
2. Agregar migracion cloud de tablas `desktop_devices`, `synced_*` y `cloud_change_log`.
3. Implementar tests SQL/RLS de escritura bloqueada y lectura admin.
4. Implementar endpoint cloud de ingesta con idempotencia.
5. Implementar cliente HTTP desktop y configuracion protegida.
6. Encolar eventos reales de tickets y pagos.
7. Implementar pull incremental de citas y catalogo hacia SQLite.
8. Convertir `/admin/sync` en consola operativa real.
9. Agregar tests de conflicto y reintentos.
10. Actualizar `phase-2-current-status.md`, arquitectura y README de `Barberia.ApiClient`/`Barberia.Sync`.

## Reglas De Conflicto Iniciales

- Ticket local duplicado: gana el primer evento procesado por `idempotency_key`; duplicados se ignoran.
- Pago local duplicado: se bloquea por `local_payment_id` y se marca conflicto si el monto difiere.
- Pago sin ticket: queda en conflicto abierto hasta mapear o ignorar.
- Servicio editado en web y local: gana Supabase para catalogo futuro; pagos historicos mantienen nombre/precio snapshot local.
- Barbero desactivado en web con operacion local activa: no se cancela el ticket local; se bloquean nuevos flujos y se registra conflicto operativo.
- Cita cloud futura vs ticket walk-in local: la asignacion local debe proteger al barbero 15 minutos antes; si ya hubo choque offline, registrar conflicto.
- Cita cancelada en cloud mientras desktop estaba offline: al hacer pull, desktop deja de proteger ese bloque futuro.

## Criterios De Cierre

- Desktop puede operar sin internet y acumular eventos en `sync_outbox_events`.
- Al volver internet, los eventos se envian por lotes y no se duplican al reintentar.
- Supabase recibe y materializa tickets, pagos y tiempos de ciclo de vida.
- Admin web ve historial sync, errores y conflictos reales.
- Desktop descarga citas futuras y catalogo cloud sin bloquear la operacion local.
- Una cita web confirmada protege al barbero en kiosco, pantalla publica, barbero y autocaja.
- Conflictos quedan visibles y auditables.
- RLS impide escrituras directas no autorizadas sobre tablas de sync/POS.
- Tests de idempotencia, RLS, reintentos y conflictos pasan.

## Validacion

- Supabase:
  - aplicar migraciones en entorno local o real;
  - tests SQL/RLS para admin, customer, barber y dispositivo;
  - tests de ingesta duplicada;
  - tests de eventos fuera de orden;
  - tests de conflictos.
- Web:
  - `npm run typecheck`
  - `npm run lint`
  - `npm run build`
  - tests de `/admin/sync`.
- Desktop:
  - `dotnet build src\desktop\Barberia.Desktop\Barberia.Desktop.csproj --no-restore`
  - tests de `Barberia.Sync` y `Barberia.ApiClient` cuando existan.
  - prueba manual: operar offline, cerrar tickets, registrar pagos, reconectar y verificar sync.

Si la validacion genera `.codex-test-output/`, borrar esa carpeta al terminar.

## Fuera De Alcance

- Depositos online y Stripe.
- Sincronizacion multi-sucursal.
- Reemplazar reportes locales por reportes cloud.
- Hacer que Windows dependa de Supabase para abrir kiosco, barbero, pantalla publica o autocaja.
- Resolver automaticamente todos los conflictos de catalogo.

## Riesgos Y Preguntas Antes De Implementar

- Autenticacion de dispositivo: decidir si se usara Edge Function con secreto por dispositivo, JWT emitido por backend o service role solo del lado cloud.
- Mapeo de catalogo: confirmar si barberos/servicios locales actuales deben migrarse una vez hacia Supabase o mapearse manualmente desde admin.
- Pagos: confirmar si Fase 2.5 sincroniza solo efectivo local o tambien otros metodos capturados por `payment_method`.
- Imagenes: confirmar si la cache local de imagenes de barberos entra en 2.5 o queda para una subfase posterior.
- Resolucion de conflictos: confirmar si admin puede elegir "desktop wins" para catalogo, porque eso contradice la autoridad cloud definida para Fase 2.

## Referencias Oficiales

- Supabase Edge Functions: https://supabase.com/docs/guides/functions
- Supabase Row Level Security: https://supabase.com/docs/guides/database/postgres/row-level-security
- Windows App SDK: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/
