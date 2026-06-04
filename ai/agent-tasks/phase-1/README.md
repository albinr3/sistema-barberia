# Tareas Para Agentes - Fase 1

## Antes De Ejecutar Cualquier Tarea

1. Leer `docs/fases/phase-1-current-status.md`.
2. Buscar la primera tarea que no este `completed`.
3. Confirmar que `git status --short` esta limpio.
4. Crear una rama nueva desde `main` antes de modificar archivos, aunque el usuario no lo pida explicitamente.
5. Usar la rama recomendada en la tabla; si ya existe o falta, crear una variante descriptiva con formato `phase-1/NN-descripcion`.
6. No implementar tareas de fase directamente sobre `main`.
7. Ejecutar unicamente el archivo guia de esa tarea.
8. Al terminar, correr el comando de verificacion.
9. Actualizar `docs/fases/phase-1-current-status.md`.
10. Marcar la tarea como `completed`, `partial` o `blocked`.
11. No avanzar a la siguiente tarea en la misma sesion sin aprobacion humana.

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
- Para cualquier pantalla WinUI, respetar `docs/diseno/desktop-visual-theme.md` y `ai/instructions/desktop-winui.md`.

