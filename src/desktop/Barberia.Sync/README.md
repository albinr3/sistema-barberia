# Barberia.Sync

Libreria para cola y sincronizacion futura no bloqueante.

Responsabilidades futuras:

- Cola de eventos locales.
- Reintentos cuando exista conectividad.
- Coordinacion de sincronizacion eventual sin bloquear la operacion local.

Restricciones actuales:

- Depende de `Barberia.Core`.
- No configura Supabase.
- No implementa cola, reintentos ni reglas de sincronizacion.
