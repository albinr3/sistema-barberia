# 10 - Modulo De Autocaja

## Objetivo

Implementar el modulo local de autocaja operada por el barbero para cerrar servicios pagados en efectivo.

## Archivos/Carpetas Permitidas

- `src/desktop/Barberia.Desktop/`
- `src/desktop/Barberia.Core/`
- `src/desktop/Barberia.Data/`
- `src/desktop/Barberia.Hardware/` mediante interfaces aprobadas.
- `tests/desktop/Barberia.Core.Tests/`
- `tests/desktop/Barberia.Hardware.Tests/`
- `docs/decisiones/decision-log.md`

## Archivos/Carpetas Prohibidas

- `src/cloud/`
- `src/mobile/`
- Booking web.
- App movil.
- Pagos con tarjeta.
- Pagos online.

## Contexto Obligatorio A Leer Antes De Trabajar

- `ai/context/autocaja-flow.md`
- `ai/context/barber-states.md`
- `ai/context/turn-assignment-rules.md`
- `ai/context/business-rules.md`
- `docs/hardware/pos-hardware-notes.md`
- `docs/arquitectura/phase-1-technical-plan.md`

## Resultado Esperado

Autocaja local que valida ticket/barbero, registra monto en efectivo, calcula comision, imprime constancia, abre cash drawer y completa el turno.

## Criterios De Aceptacion

- Solo efectivo en Fase 1.
- No hay precio sugerido.
- El turno pasa a `completed`.
- El barbero vuelve a `available`.
- El barbero pasa al final de la cola rotativa.
- Se registran datos auditables minimos.
- Errores de hardware quedan manejados mediante abstracciones.

## Cosas Que NO Debe Hacer El Agente

- No implementar pagos con tarjeta.
- No implementar depositos online.
- No cerrar servicio desde panel de barbero.
- No acoplar UI directamente a drivers de hardware.
- No implementar booking web ni app movil.

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
