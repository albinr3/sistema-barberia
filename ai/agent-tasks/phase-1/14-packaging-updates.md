# 14 - Packaging Y Actualizaciones

## Objetivo

Preparar empaquetado y estrategia de actualizaciones para la aplicacion Windows local de Fase 1.

## Archivos/Carpetas Permitidas

- `src/desktop/Barberia.Desktop/`
- Archivos de packaging aprobados para Windows.
- Documentacion tecnica relacionada en `docs/`.
- `docs/decisiones/decision-log.md`

## Archivos/Carpetas Prohibidas

- `src/cloud/`
- `src/mobile/`
- Booking web.
- App movil.
- Scripts destructivos o instaladores no aprobados.

## Contexto Obligatorio A Leer Antes De Trabajar

- `ai/context/project-overview.md`
- `ai/context/business-rules.md`
- `docs/arquitectura/phase-1-technical-plan.md`
- `docs/arquitectura/phase-1-solution-structure.md`
- `ai/instructions/desktop-winui.md`
- `docs/diseno/desktop-visual-theme.md`

## Resultado Esperado

Estrategia de packaging local documentada e implementada solo con aprobacion, lista para instalar o actualizar la app Windows.

## Criterios De Aceptacion

- No cambia el tema visual desktop aprobado salvo decision registrada en `docs/decisiones/decision-log.md`.
- El packaging no requiere internet para operar despues de instalado.
- No rompe la base SQLite local existente.
- Define como tratar configuracion, logs y datos locales durante actualizaciones.
- No incluye componentes de Fase 2 o Fase 3.
- Cualquier decision nueva queda en el decision log.

## Cosas Que NO Debe Hacer El Agente

- No publicar instaladores sin aprobacion.
- No borrar datos locales durante actualizaciones.
- No crear backend cloud.
- No crear booking web.
- No crear app movil.

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
