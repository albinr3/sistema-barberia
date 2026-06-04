# 01 - Crear Solucion .NET

## Objetivo

Crear la solucion .NET inicial y los proyectos vacios de Fase 1 segun la estructura aprobada.

## Archivos/Carpetas Permitidas

- `BarberiaSystem.sln`
- `src/desktop/Barberia.Desktop/`
- `src/desktop/Barberia.Core/`
- `src/desktop/Barberia.Data/`
- `src/desktop/Barberia.Sync/`
- `src/desktop/Barberia.Hardware/`
- `src/desktop/Barberia.ApiClient/`
- `src/desktop/Barberia.Shared/`
- `tests/desktop/Barberia.Core.Tests/`
- `tests/desktop/Barberia.Data.Tests/`
- `tests/desktop/Barberia.Sync.Tests/`
- `tests/desktop/Barberia.Hardware.Tests/`
- `docs/decisiones/decision-log.md`

## Archivos/Carpetas Prohibidas

- `src/cloud/`
- `src/mobile/`
- Cualquier proyecto de booking web.
- Cualquier proyecto de app movil.
- Migraciones de base de datos.
- Archivos de implementacion de pantallas.

## Contexto Obligatorio A Leer Antes De Trabajar

- `ai/context/project-overview.md`
- `ai/context/business-rules.md`
- `docs/arquitectura/phase-1-solution-structure.md`
- `docs/fases/phase-1-implementation-sequence.md`
- `ai/instructions/desktop-winui.md`
- `ai/instructions/testing.md`

## Resultado Esperado

Solucion y proyectos base creados, sin logica de negocio ni pantallas funcionales.

## Criterios De Aceptacion

- Existe `BarberiaSystem.sln`.
- Existen los proyectos listados en la estructura aprobada.
- Las referencias entre proyectos respetan los limites definidos.
- `Barberia.Core` no tiene referencias a WinUI, SQLite, EF Core, Supabase, hardware ni APIs.
- No se implementa funcionalidad de negocio.

## Cosas Que NO Debe Hacer El Agente

- No implementar motor de turnos.
- No crear pantallas.
- No crear base de datos real.
- No instalar paquetes no necesarios para el scaffolding aprobado.
- No implementar booking web.
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
