# 12 - Foundation De Sincronizacion

## Objetivo

Implementar la base local de sincronizacion no bloqueante mediante cola de eventos y reintentos futuros.

## Archivos/Carpetas Permitidas

- `src/desktop/Barberia.Sync/`
- `src/desktop/Barberia.ApiClient/`
- `src/desktop/Barberia.Data/` solo para persistir cola/eventos locales.
- `src/desktop/Barberia.Core/` solo para contratos de eventos de dominio aprobados.
- `tests/desktop/Barberia.Sync.Tests/`
- `docs/decisiones/decision-log.md`

## Archivos/Carpetas Prohibidas

- `src/cloud/`
- `src/mobile/`
- Booking web.
- App movil.
- Migraciones cloud reales.
- Supabase Auth.

## Contexto Obligatorio A Leer Antes De Trabajar

- `ai/context/business-rules.md`
- `ai/context/appointment-rules.md`
- `docs/arquitectura/phase-1-technical-plan.md`
- `docs/arquitectura/phase-1-solution-structure.md`
- `ai/instructions/supabase.md`
- `ai/instructions/backend-cloud.md`
- `ai/instructions/testing.md`

## Resultado Esperado

Sync foundation que registre eventos locales y permita reintentos sin bloquear flujos de Fase 1.

## Criterios De Aceptacion

- La operacion local funciona sin internet.
- Fallas de API no impiden check-in, asignacion, panel, autocaja ni reportes locales.
- Los eventos quedan en cola local.
- Las pruebas cubren reintentos y fallas.
- La API cloud queda como cliente futuro, no autoridad local.

## Cosas Que NO Debe Hacer El Agente

- No crear backend cloud real.
- No crear booking web.
- No crear app movil.
- No implementar autenticacion de clientes.
- No hacer que la nube bloquee SQLite local.

