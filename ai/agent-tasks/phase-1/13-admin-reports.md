# 13 - Reportes Administrativos Locales

## Objetivo

Implementar reportes administrativos locales para Fase 1 usando datos de SQLite y reglas aprobadas.

## Archivos/Carpetas Permitidas

- `src/desktop/Barberia.Desktop/`
- `src/desktop/Barberia.Data/`
- `src/desktop/Barberia.Core/` solo para reglas o contratos aprobados.
- `tests/desktop/Barberia.Data.Tests/`
- `docs/decisiones/decision-log.md`

## Archivos/Carpetas Prohibidas

- `src/cloud/`
- `src/mobile/`
- Booking web.
- App movil.
- Exportaciones Excel/PDF si no estan confirmadas.

## Contexto Obligatorio A Leer Antes De Trabajar

- `ai/context/business-rules.md`
- `ai/context/autocaja-flow.md`
- `ai/context/glossary.md`
- `docs/arquitectura/phase-1-technical-plan.md`
- `ai/instructions/desktop-winui.md`
- `ai/instructions/testing.md`

## Resultado Esperado

Reportes locales basicos para operaciones, pagos en efectivo y comisiones, sin depender de internet.

## Criterios De Aceptacion

- Los reportes leen de la base local.
- No requieren sincronizacion cloud para funcionar.
- No asumen exportacion Excel/PDF si sigue pendiente.
- El calculo de comision respeta la regla vigente o usa placeholder claro si falta confirmacion.
- No duplican logica que deba vivir en Core.

## Cosas Que NO Debe Hacer El Agente

- No crear panel admin web.
- No crear app movil.
- No implementar reportes cloud.
- No inventar reglas finales de comision si siguen pendientes.
- No agregar exportaciones no aprobadas.

