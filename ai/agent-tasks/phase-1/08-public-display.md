# 08 - Pantalla Publica

## Objetivo

Implementar la pantalla publica local para mostrar turnos, llamados, barberos y reservas protegidas.

## Archivos/Carpetas Permitidas

- `src/desktop/Barberia.Desktop/`
- `src/desktop/Barberia.Core/` solo para contratos existentes o ajustes aprobados.
- `src/desktop/Barberia.Data/` solo para consultas locales ya definidas.
- `docs/decisiones/decision-log.md`

## Archivos/Carpetas Prohibidas

- `src/cloud/`
- `src/mobile/`
- Booking web.
- App movil.
- Autenticacion cloud.

## Contexto Obligatorio A Leer Antes De Trabajar

- `ai/context/business-rules.md`
- `ai/context/barber-states.md`
- `ai/context/appointment-rules.md`
- `ai/context/turn-assignment-rules.md`
- `ai/context/glossary.md`
- `ai/instructions/desktop-winui.md`

## Resultado Esperado

Pantalla publica que diferencia visualmente walk-ins y citas programadas sincronizadas, incluyendo estado de barbero reservado por cita proxima.

## Criterios De Aceptacion

- Muestra informacion desde estado local.
- Diferencia walk-ins de citas.
- Puede mostrar "Reservado para cita" o equivalente cuando aplique proteccion.
- No requiere internet.
- No implementa booking web.

## Cosas Que NO Debe Hacer El Agente

- No crear flujo para reservar citas.
- No crear cuenta de usuario cliente.
- No implementar app movil.
- No duplicar estados como strings en UI.
- No llamar APIs para refrescar la pantalla como requisito operativo.

