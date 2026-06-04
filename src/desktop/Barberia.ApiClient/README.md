# Barberia.ApiClient

Libreria para comunicacion cloud futura.

Responsabilidades futuras:

- Cliente API/cloud aislado de la operacion local.
- Contratos de comunicacion remota cuando esten aprobados.

Restricciones actuales:

- Depende de `Barberia.Core`.
- No configura Supabase.
- No implementa llamadas HTTP ni integraciones cloud.
- No debe bloquear flujos locales de Fase 1.
- Expone contratos minimos de sync futuro y un cliente no disponible para que `Barberia.Sync` pueda reintentar sin depender de una API real.
