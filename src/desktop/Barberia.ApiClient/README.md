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
