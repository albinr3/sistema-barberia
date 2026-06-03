# Estructura De Solucion Para Fase 1

## Nota De Estado

El skeleton de solucion .NET para Fase 1 ya fue creado y mergeado a `main`. Este documento sigue siendo referencia arquitectonica para estructura, responsabilidades y dependencias; el tracking real de progreso vive en `docs/fases/phase-1-current-status.md`.

## Solucion

La solucion se llama:

```text
BarberiaSystem.sln
```

Este documento describe la estructura aprobada. La implementacion funcional de cada paso debe seguir `docs/fases/phase-1-current-status.md` y los prompts de `ai/agent-tasks/phase-1/`.

## Proyectos Desktop

```text
src/desktop/Barberia.Desktop
src/desktop/Barberia.Core
src/desktop/Barberia.Data
src/desktop/Barberia.Sync
src/desktop/Barberia.Hardware
src/desktop/Barberia.ApiClient
src/desktop/Barberia.Shared
```

### Barberia.Desktop

Responsable de la aplicacion WinUI 3, navegacion, composicion de modulos visuales y wiring de dependencias.

Contiene pantallas futuras para kiosco, pantalla publica, panel de barbero, autocaja, administracion local y reportes. No contiene reglas de negocio de asignacion de turnos ni logica de persistencia.

### Barberia.Core

Responsable de reglas de negocio puras.

Debe contener:

- Motor de asignacion de turnos.
- Estados centrales de barbero y turno.
- Reglas de disponibilidad local.
- Reglas de cola rotativa.
- Reglas de cierre operativo que afecten dominio.
- Modelos de dominio que no dependan de infraestructura.

Reglas obligatorias:

- No depende de WinUI.
- No depende de SQLite.
- No depende de EF Core.
- No depende de Supabase.
- No depende de hardware.
- No depende de APIs.
- Debe poder probarse con pruebas unitarias rapidas.

### Barberia.Data

Responsable de SQLite y persistencia local.

Debe contener repositorios, mapeos, transacciones y acceso a datos locales. No debe decidir reglas de asignacion ni estados por su cuenta; debe persistir decisiones hechas por Core o flujos coordinados desde la aplicacion.

### Barberia.Sync

Responsable de cola de eventos y sincronizacion futura.

Debe permitir registrar eventos locales y reintentarlos cuando exista conectividad. No debe impedir que kiosco, panel de barbero, autocaja o reportes locales funcionen sin internet.

### Barberia.Hardware

Responsable de abstraer hardware POS.

Debe cubrir, mediante interfaces y adaptadores futuros:

- Impresora de tickets o constancias.
- Escaner QR.
- Cash drawer.

Las pantallas no deben hablar directamente con drivers o protocolos de hardware.

### Barberia.ApiClient

Responsable del cliente API/cloud futuro.

Debe contener comunicacion con servicios cloud cuando existan. No debe ser autoridad de la operacion local ni bloquear flujos de Fase 1.

### Barberia.Shared

Responsable de contratos compartibles solo si realmente se necesitan.

No debe convertirse en un contenedor general de utilidades. Debe usarse con criterio para contratos que deban compartirse entre proyectos sin introducir dependencias indebidas.

## Proyectos De Prueba

```text
tests/desktop/Barberia.Core.Tests
tests/desktop/Barberia.Data.Tests
tests/desktop/Barberia.Sync.Tests
tests/desktop/Barberia.Hardware.Tests
```

### Barberia.Core.Tests

Pruebas unitarias del dominio y del motor de turnos. Deben existir antes de implementar el motor de asignacion.

### Barberia.Data.Tests

Pruebas de persistencia local, transacciones, migraciones locales futuras y repositorios SQLite.

### Barberia.Sync.Tests

Pruebas de cola de eventos, reintentos, estados de sincronizacion y comportamiento no bloqueante.

### Barberia.Hardware.Tests

Pruebas de contratos, adaptadores simulados y manejo de errores de impresora, escaner QR y cash drawer.

## Reglas De Dependencia

Dependencias permitidas esperadas:

```text
Barberia.Desktop -> Barberia.Core
Barberia.Desktop -> Barberia.Data
Barberia.Desktop -> Barberia.Sync
Barberia.Desktop -> Barberia.Hardware
Barberia.Desktop -> Barberia.ApiClient

Barberia.Data -> Barberia.Core
Barberia.Sync -> Barberia.Core
Barberia.Sync -> Barberia.ApiClient
Barberia.Hardware -> Barberia.Core si necesita contratos de dominio
Barberia.ApiClient -> Barberia.Shared si existen contratos compartidos
```

Dependencias prohibidas:

```text
Barberia.Core -> Barberia.Desktop
Barberia.Core -> Barberia.Data
Barberia.Core -> Barberia.Sync
Barberia.Core -> Barberia.Hardware
Barberia.Core -> Barberia.ApiClient
Barberia.Core -> SQLite/EF Core/Supabase/WinUI/APIs/hardware
```

## Reglas De Alcance

- No implementar booking web en Fase 1.
- No implementar app movil en Fase 1.
- No duplicar estados de barbero o turno en UI.
- No crear contratos cloud prematuros si no estan aprobados.
- No convertir `Barberia.Shared` en una dependencia obligatoria para todo.

