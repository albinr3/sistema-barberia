# 07 - Modulo De Kiosco

## Objetivo

Implementar el modulo local de kiosco touch para check-in de walk-ins en Fase 1.

## Archivos/Carpetas Permitidas

- `src/desktop/Barberia.Desktop/`
- `src/desktop/Barberia.Core/` solo si faltan contratos de dominio aprobados.
- `src/desktop/Barberia.Data/` solo mediante interfaces/repositorios ya definidos.
- `tests/desktop/Barberia.Core.Tests/` si se agregan reglas.
- `docs/decisiones/decision-log.md`

## Archivos/Carpetas Prohibidas

- `src/cloud/`
- `src/mobile/`
- Booking web.
- App movil.
- Pago online.

## Contexto Obligatorio A Leer Antes De Trabajar

- `ai/context/project-overview.md`
- `ai/context/business-rules.md`
- `ai/context/turn-assignment-rules.md`
- `ai/context/glossary.md`
- `docs/arquitectura/phase-1-technical-plan.md`
- `ai/instructions/desktop-winui.md`

## Resultado Esperado

Modulo de check-in local que registra walk-ins para entrar a la cola local segun reglas de Fase 1.

## Criterios De Aceptacion

- No hay seleccion de servicio en kiosco.
- El flujo funciona sin internet.
- El registro queda preparado para persistencia local SQLite.
- La asignacion se delega al motor de Core.
- No se implementan cuentas de usuario cliente.

## Cosas Que NO Debe Hacer El Agente

- No implementar booking web.
- No implementar app movil.
- No pedir deposito ni pago online.
- No crear precio sugerido.
- No duplicar reglas de turnos en la pantalla.

