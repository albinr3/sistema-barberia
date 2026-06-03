# 04 - Motor De Asignacion De Turnos

## Objetivo

Implementar el motor de asignacion en `Barberia.Core` para satisfacer las pruebas escritas en la tarea 03.

## Archivos/Carpetas Permitidas

- `src/desktop/Barberia.Core/`
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

- `ai/context/turn-assignment-rules.md`
- `ai/context/barber-states.md`
- `ai/context/appointment-rules.md`
- `ai/context/autocaja-flow.md`
- `docs/arquitectura/phase-1-solution-structure.md`
- `ai/agent-tasks/phase-1/03-turn-assignment-tests.md`

## Resultado Esperado

Motor puro de dominio que asigna turnos segun seleccion de barbero, disponibilidad, prioridad de 0 clientes, cola rotativa y proteccion por cita proxima.

## Criterios De Aceptacion

- Todas las pruebas de `Barberia.Core.Tests` pasan.
- Core sigue sin dependencias de UI, datos, hardware o APIs.
- La asignacion usa estados centrales, no strings duplicados.
- El cierre de autocaja puede mover al barbero al final de la cola rotativa como regla de dominio.
- La proteccion por cita proxima existe como entrada/regla, sin implementar booking web.

## Cosas Que NO Debe Hacer El Agente

- No mover reglas a WinUI.
- No persistir directamente en SQLite.
- No llamar APIs cloud.
- No crear pantallas.
- No implementar administracion completa de citas.
- No implementar app movil.

