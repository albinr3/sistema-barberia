# Barberia.Sync

Libreria para cola y sincronizacion futura no bloqueante.

Responsabilidades futuras:

- Cola de eventos locales.
- Reintentos cuando exista conectividad.
- Coordinacion de sincronizacion eventual sin bloquear la operacion local.

Restricciones actuales:

- Depende de `Barberia.Core`, `Barberia.Data` para el outbox SQLite local y `Barberia.ApiClient` para el contrato cloud futuro.
- No configura Supabase.
- No implementa HTTP, autenticacion ni backend cloud.
- Registra eventos locales en una cola outbox y procesa reintentos sin bloquear los flujos locales.
