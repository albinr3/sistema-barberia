# 11 - Abstracciones De Hardware

## Objetivo

Definir e implementar interfaces y adaptadores simulados para impresora, escaner QR y cash drawer.

## Archivos/Carpetas Permitidas

- `src/desktop/Barberia.Hardware/`
- `src/desktop/Barberia.Core/` solo para contratos de dominio necesarios.
- `tests/desktop/Barberia.Hardware.Tests/`
- `docs/hardware/pos-hardware-notes.md`
- `docs/decisiones/decision-log.md`

## Archivos/Carpetas Prohibidas

- `src/cloud/`
- `src/mobile/`
- Booking web.
- App movil.
- UI directa de hardware fuera de contratos.

## Contexto Obligatorio A Leer Antes De Trabajar

- `docs/hardware/pos-hardware-notes.md`
- `ai/context/autocaja-flow.md`
- `ai/context/business-rules.md`
- `docs/arquitectura/phase-1-solution-structure.md`
- `ai/instructions/testing.md`

## Resultado Esperado

Contratos de hardware y simuladores que permitan probar flujos sin depender de dispositivos reales.

## Criterios De Aceptacion

- Existen interfaces para impresora, escaner QR y cash drawer.
- Las pruebas cubren exito y fallos principales.
- La UI no habla directamente con hardware.
- Core no depende de drivers ni protocolos.
- Los modelos reales de hardware quedan documentados como pendientes si no se conocen.

## Cosas Que NO Debe Hacer El Agente

- No instalar drivers sin aprobacion.
- No asumir modelos de hardware no confirmados.
- No bloquear autocaja por cloud.
- No crear booking web.
- No crear app movil.

