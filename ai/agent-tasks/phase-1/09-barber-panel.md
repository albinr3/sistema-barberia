# 09 - Panel De Barbero

## Objetivo

Implementar el panel local de barbero para check-in operativo, estado disponible, escaneo de ticket e inicio de atencion.

## Archivos/Carpetas Permitidas

- `src/desktop/Barberia.Desktop/`
- `src/desktop/Barberia.Core/` solo para contratos/reglas aprobadas.
- `src/desktop/Barberia.Data/` solo mediante servicios locales existentes.
- `src/desktop/Barberia.Hardware/` solo para contratos de escaner aprobados.
- `tests/desktop/Barberia.Core.Tests/` si se agregan reglas.
- `docs/decisiones/decision-log.md`

## Archivos/Carpetas Prohibidas

- `src/cloud/`
- `src/mobile/`
- Booking web.
- App movil.
- Pagos online.

## Contexto Obligatorio A Leer Antes De Trabajar

- `ai/context/barber-states.md`
- `ai/context/turn-assignment-rules.md`
- `ai/context/autocaja-flow.md`
- `ai/context/business-rules.md`
- `docs/arquitectura/phase-1-technical-plan.md`
- `ai/instructions/desktop-winui.md`
- `docs/diseno/desktop-visual-theme.md`

## Resultado Esperado

Panel que permite al barbero participar en el flujo local sin cerrar servicios desde el panel.

## Criterios De Aceptacion

- Respeta `docs/diseno/desktop-visual-theme.md` y reutiliza patrones visuales de la shell WinUI existente.
- Solo barberos `available` reciben walk-ins.
- Escanear ticket asignado mueve turno y barbero a `in_service`.
- El panel no tiene boton obligatorio de terminar servicio.
- El cierre operativo queda para autocaja.
- No hay dependencia de internet.

## Cosas Que NO Debe Hacer El Agente

- No cerrar servicios desde el panel.
- No mover al barbero al final de cola fuera del cierre de autocaja.
- No implementar booking web.
- No implementar app movil.
- No duplicar estados en UI.

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
