# 05 - Capa De Datos SQLite

## Objetivo

Implementar la persistencia local SQLite para operaciones de Fase 1, manteniendo SQLite como primera escritura y sin mover reglas de negocio fuera de Core.

## Archivos/Carpetas Permitidas

- `src/desktop/Barberia.Data/`
- `src/desktop/Barberia.Core/` solo para contratos estrictamente necesarios.
- `tests/desktop/Barberia.Data.Tests/`
- `docs/decisiones/decision-log.md`

## Archivos/Carpetas Prohibidas

- `src/desktop/Barberia.Desktop/` salvo wiring posterior aprobado.
- `src/cloud/`
- `src/mobile/`
- Proyectos Supabase reales.
- Migraciones cloud.

## Contexto Obligatorio A Leer Antes De Trabajar

- `ai/context/business-rules.md`
- `ai/context/barber-states.md`
- `ai/context/turn-assignment-rules.md`
- `ai/context/autocaja-flow.md`
- `docs/arquitectura/phase-1-technical-plan.md`
- `docs/arquitectura/phase-1-solution-structure.md`
- `ai/instructions/testing.md`

## Resultado Esperado

Capa local de persistencia preparada para turnos, barberos, estados, pagos en efectivo, eventos auditables y consultas locales necesarias.

## Criterios De Aceptacion

- SQLite es la primera escritura para operacion local.
- Las reglas de negocio permanecen en Core.
- Las pruebas de persistencia cubren operaciones criticas y transacciones.
- No se requiere internet para guardar cambios locales.
- No hay dependencia directa de UI.

## Cosas Que NO Debe Hacer El Agente

- No implementar Supabase/PostgreSQL.
- No crear booking web.
- No crear app movil.
- No decidir reglas de asignacion dentro de repositorios.
- No bloquear flujos locales por sync.

