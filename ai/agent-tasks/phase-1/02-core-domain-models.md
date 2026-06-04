# 02 - Modelos De Dominio Core

## Objetivo

Definir los modelos de dominio iniciales para turnos, barberos, estados y conceptos minimos necesarios para probar reglas de Fase 1.

## Archivos/Carpetas Permitidas

- `src/desktop/Barberia.Core/`
- `src/desktop/Barberia.Shared/` solo si existe una necesidad real de contratos compartidos.
- `tests/desktop/Barberia.Core.Tests/`
- `docs/decisiones/decision-log.md`

## Archivos/Carpetas Prohibidas

- `src/desktop/Barberia.Desktop/`
- `src/desktop/Barberia.Data/`
- `src/desktop/Barberia.Sync/`
- `src/desktop/Barberia.Hardware/`
- `src/desktop/Barberia.ApiClient/`
- `src/cloud/`
- `src/mobile/`

## Contexto Obligatorio A Leer Antes De Trabajar

- `ai/context/business-rules.md`
- `ai/context/barber-states.md`
- `ai/context/turn-assignment-rules.md`
- `ai/context/appointment-rules.md`
- `docs/arquitectura/phase-1-technical-plan.md`
- `docs/arquitectura/phase-1-solution-structure.md`

## Resultado Esperado

Modelos y estados centrales definidos en Core o Shared segun corresponda, listos para pruebas del motor.

## Criterios De Aceptacion

- Estados de barbero y turno existen en una ubicacion central.
- No hay duplicacion de estados para UI.
- Los modelos no dependen de infraestructura.
- Los nombres reflejan el glosario y reglas confirmadas.
- Las pruebas de compilacion del proyecto Core pasan.

## Cosas Que NO Debe Hacer El Agente

- No implementar asignacion de turnos todavia.
- No implementar UI.
- No implementar SQLite.
- No agregar dependencias externas a Core.
- No modelar booking web completo.
- No implementar app movil.

## Cierre Obligatorio

Antes de finalizar, el agente debe actualizar:

- `docs/fases/phase-1-current-status.md`

Debe registrar:

- Estado final de esta tarea: `completed`, `partial` o `blocked`.
- Fecha de ultimo cambio.
- Evidencia concreta: archivos creados/modificados, pruebas ejecutadas y resultado.
- Proximo paso recomendado.
- Notas si hubo fallos de verificacion o limitaciones.

No responder como tarea completada si este archivo no fue actualizado.
