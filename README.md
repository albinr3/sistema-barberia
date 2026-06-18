# Sistema Barberia

Repositorio base para un sistema de barberia organizado como monorepo.

## Organizacion

Este repositorio se organiza por componentes tecnicos, no por carpetas de fase:

- `src/desktop`: aplicacion local Windows.
- `src/cloud`: servicios cloud y backend futuro.
- `src/mobile`: aplicacion movil futura.
- `tests`: pruebas por componente y pruebas end-to-end.
- `database`: recursos futuros de SQLite, Supabase y migraciones.
- `docs`: documentacion de alcance, arquitectura, decisiones, hardware y roadmaps.
- `ai`: instrucciones, contexto y tareas para agentes de IA.

## Fases

Las fases del producto se gestionaran con milestones, labels e issues, no con carpetas separadas.

- Fase 1: aplicacion Windows local.
- Fase 2: booking web, panel admin web y cuenta web.
- Fase 3: app movil iOS/Android.

## Estado actual

El repositorio ya contiene la estructura base del monorepo, contexto IA en Markdown, plan tecnico de Fase 1 y solucion .NET para Fase 1. La aplicacion Windows local usa WinUI 3 con ventanas y paginas declaradas en XAML por defecto; la regla se protege con pruebas en `tests/desktop/Barberia.Desktop.Tests`.

## Deploy web desde la raiz

La app Next.js vive en `src/web/barberia-web`. Para proveedores que despliegan desde la raiz del repositorio y no permiten cambiar el directorio base, el `package.json` raiz expone:

- `npm run build`: instala dependencias de `src/web/barberia-web` y ejecuta el build de Next.js.
- `npm start`: inicia la app web con `next start` desde `src/web/barberia-web`.

En Hostinger, el directorio de salida del build debe apuntar a `src/web/barberia-web/.next`, porque Next.js genera los artefactos dentro de la subcarpeta de la app web.

El estado vivo de Fase 1 esta en:

- `docs/fases/phase-1-current-status.md`

Antes de abrir una nueva tarea se debe revisar ese archivo para detectar el proximo paso. Las tareas de Fase 1 se ejecutan usando los prompts de:

- `ai/agent-tasks/phase-1/`

