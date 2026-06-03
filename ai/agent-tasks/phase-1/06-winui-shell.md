# 06 - Shell WinUI

## Objetivo

Crear la shell inicial de WinUI 3 para hospedar modulos visuales de Fase 1 sin implementar todavia flujos completos.

## Archivos/Carpetas Permitidas

- `src/desktop/Barberia.Desktop/`
- `src/desktop/Barberia.Shared/` solo si se necesita un contrato compartido aprobado.
- `docs/decisiones/decision-log.md`

## Archivos/Carpetas Prohibidas

- `src/cloud/`
- `src/mobile/`
- `src/desktop/Barberia.Core/` salvo consumo de contratos existentes.
- `src/desktop/Barberia.Data/` salvo configuracion de composicion aprobada.

## Contexto Obligatorio A Leer Antes De Trabajar

- `ai/context/project-overview.md`
- `ai/context/business-rules.md`
- `ai/context/glossary.md`
- `docs/arquitectura/phase-1-solution-structure.md`
- `ai/instructions/desktop-winui.md`

## Resultado Esperado

Shell navegable para modulos locales de Fase 1, con composicion preparada y sin reglas de negocio embebidas.

## Criterios De Aceptacion

- La shell compila.
- No duplica estados de barbero o turno.
- No implementa booking web ni app movil.
- No contiene reglas de asignacion.
- Los modulos futuros quedan separados por responsabilidad visual.

## Cosas Que NO Debe Hacer El Agente

- No implementar motor de turnos en UI.
- No crear base de datos.
- No conectar hardware directo desde pantallas.
- No crear formularios web.
- No crear app movil.

