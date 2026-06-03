# 03 - Pruebas Del Motor De Asignacion De Turnos

## Objetivo

Escribir pruebas unitarias del motor de asignacion antes de implementar el motor. Las pruebas deben capturar las reglas confirmadas de turnos y proteger el comportamiento del dominio.

## Archivos/Carpetas Permitidas

- `tests/desktop/Barberia.Core.Tests/`
- `src/desktop/Barberia.Core/` solo para ajustes minimos de contratos necesarios para compilar pruebas.
- `docs/decisiones/decision-log.md`

## Archivos/Carpetas Prohibidas

- `src/desktop/Barberia.Desktop/`
- `src/desktop/Barberia.Data/`
- `src/desktop/Barberia.Sync/`
- `src/desktop/Barberia.Hardware/`
- `src/desktop/Barberia.ApiClient/`
- `src/cloud/`
- `src/mobile/`
- Migraciones o base de datos real.

## Contexto Obligatorio A Leer Antes De Trabajar

- `ai/context/turn-assignment-rules.md`
- `ai/context/barber-states.md`
- `ai/context/appointment-rules.md`
- `ai/context/autocaja-flow.md`
- `ai/context/business-rules.md`
- `docs/arquitectura/phase-1-technical-plan.md`
- `docs/arquitectura/phase-1-solution-structure.md`
- `ai/instructions/testing.md`

## Resultado Esperado

Suite de pruebas unitarias que falle inicialmente si el motor aun no existe o no cumple las reglas.

## Criterios De Aceptacion

Las pruebas deben cubrir:

- Seleccion de barbero especifico.
- Seleccion de varios barberos.
- Seleccion de cualquiera.
- Prioridad de barberos compatibles con 0 clientes atendidos.
- Desempate por orden de llegada o cola inicial cuando varios compatibles tienen 0 clientes.
- Cola rotativa despues de la primera vuelta.
- No favorecer menor cantidad total de clientes si el barbero ya no esta en 0.
- Exclusion de barberos en `not_checked_in`, `called`, `in_service` y `offline`.
- Movimiento del barbero al final de la cola cuando cierra en autocaja.
- Bloqueo de barbero por cita confirmada proxima, aunque la gestion completa de citas sea Fase 2.

Tambien deben verificar que el turno mas antiguo en `waiting` sea el candidato inicial de asignacion.

## Cosas Que NO Debe Hacer El Agente

- No implementar el motor completo en esta tarea.
- No crear UI para probar reglas.
- No usar SQLite para pruebas de Core.
- No depender de servicios cloud.
- No implementar booking web.
- No implementar app movil.

