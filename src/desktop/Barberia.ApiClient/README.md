# Barberia.ApiClient

Libreria para contratos y comunicacion cloud usada por la sincronizacion Desktop-Supabase.

Responsabilidades actuales:

- Mantener el contrato `ICloudSyncClient` consumido por `Barberia.Sync`.
- Enviar eventos locales a la Edge Function `sync-events`.
- Descargar cambios desde la Edge Function `sync-changes`.
- Aislar detalles HTTP, headers de dispositivo y serializacion JSON fuera de la operacion local.

Restricciones actuales:

- Depende de `Barberia.Core`.
- No configura Supabase por si mismo; la configuracion se carga en Desktop desde `sync-settings.json`.
- No debe bloquear flujos locales si cloud no esta configurado o no esta disponible.
- Mantiene `UnavailableCloudSyncClient` para entornos donde la sincronizacion este deshabilitada.
