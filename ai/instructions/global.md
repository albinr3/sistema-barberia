# Instrucciones Globales Para Agentes

## Proposito

Guia base para cualquier agente de IA que trabaje en este repositorio.

## Reglas

- No implementar funcionalidades fuera del alcance aprobado.
- Leer `ai/context/` antes de proponer cambios.
- Registrar decisiones tecnicas en `docs/decisiones/decision-log.md`.
- Usar placeholders claros cuando falte informacion.

## Ramas Para Tareas De Fase

- Antes de ejecutar cualquier tarea de fase, el agente debe crear una rama nueva desde `main` sin esperar que el usuario lo pida explicitamente.
- La rama debe usar la columna `Rama recomendada` del tracker de la fase cuando exista.
- Si la rama recomendada ya existe o no esta definida, crear un nombre descriptivo con el formato `phase-N/NN-descripcion`.
- No empezar cambios de implementacion de una tarea de fase directamente sobre `main`.
- Si `git status --short` no esta limpio antes de crear la rama, detenerse y explicar los cambios pendientes.

## Cierre De Tareas De Fase

Si la tarea pertenece a `ai/agent-tasks/phase-1/`, antes de responder al usuario el agente debe actualizar:

- `docs/fases/phase-1-current-status.md`

La respuesta final debe mencionar que el status fue actualizado.

## Pendiente

- TODO: definir convenciones de commits y revisiones.
