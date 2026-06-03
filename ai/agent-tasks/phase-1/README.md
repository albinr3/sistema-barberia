# Tareas Para Agentes - Fase 1

## Antes De Ejecutar Cualquier Tarea

1. Leer `docs/fases/phase-1-current-status.md`.
2. Buscar la primera tarea que no este `completed`.
3. Confirmar que `git status --short` esta limpio.
4. Crear la rama recomendada en la tabla.
5. Ejecutar unicamente el archivo guia de esa tarea.
6. Al terminar, correr el comando de verificacion.
7. Actualizar `docs/fases/phase-1-current-status.md`.
8. Marcar la tarea como `completed`, `partial` o `blocked`.
9. No avanzar a la siguiente tarea en la misma sesion sin aprobacion humana.

## Proposito

Este directorio divide la implementacion futura de Fase 1 en tareas pequenas y revisables. Cada tarea debe ejecutarse solo cuando el usuario apruebe implementar esa parte.

La existencia de estos archivos no autoriza crear codigo, pantallas, paquetes, base de datos ni logica de negocio fuera de la tarea aprobada y registrada en `docs/fases/phase-1-current-status.md`.

## Orden De Trabajo

1. `01-create-dotnet-solution.md`
2. `02-core-domain-models.md`
3. `03-turn-assignment-tests.md`
4. `04-turn-assignment-engine.md`
5. `05-sqlite-data-layer.md`
6. `06-winui-shell.md`
7. `07-kiosk-module.md`
8. `08-public-display.md`
9. `09-barber-panel.md`
10. `10-autocaja-module.md`
11. `11-hardware-abstractions.md`
12. `12-sync-foundation.md`
13. `13-admin-reports.md`
14. `14-packaging-updates.md`

## Reglas Generales

- Leer el contexto obligatorio antes de editar.
- No saltar tareas si una tarea previa define contratos necesarios.
- No mezclar booking web de Fase 2.
- No mezclar app movil de Fase 3.
- Registrar decisiones tecnicas nuevas en `docs/decisiones/decision-log.md`.
- Mantener `Barberia.Core` libre de dependencias externas de UI, datos, hardware y APIs.

