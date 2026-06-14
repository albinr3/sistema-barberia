# Barberia.Sync

Libreria para cola y sincronizacion no bloqueante entre Desktop y Supabase.

Responsabilidades actuales:

- Registrar eventos locales en el outbox SQLite.
- Enviar eventos vencidos por medio de `ICloudSyncClient`.
- Marcar eventos como enviados o fallidos para permitir reintentos.
- Mantener la operacion local funcionando aunque la nube falle.

Restricciones actuales:

- Depende de `Barberia.Core`, `Barberia.Data` para el outbox SQLite local y `Barberia.ApiClient` para el contrato cloud.
- No configura Supabase; Desktop carga `sync-settings.json` y decide si iniciar la sincronizacion.
- No conoce reglas de negocio de citas, turnos o caja; solo entrega eventos y reintentos.
- El pull de cambios y la resolucion de mappings se coordinan desde `DesktopSyncService`.
