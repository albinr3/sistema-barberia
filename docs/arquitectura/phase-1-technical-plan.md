# Plan Tecnico De Fase 1

## Nota De Estado

El skeleton de solucion .NET para Fase 1 ya fue creado y mergeado a `main`. Este documento sigue siendo referencia arquitectonica para la implementacion; el tracking real de progreso vive en `docs/fases/phase-1-current-status.md`.

## Objetivo Tecnico

Preparar la primera implementacion operativa del sistema local Windows para la barberia, manteniendo una arquitectura offline-first donde la operacion diaria no dependa de internet ni de servicios cloud.

Fase 1 debe permitir operar el flujo local desde check-in hasta cierre de servicio por autocaja: kiosco, turnos automaticos, pantalla publica, panel de barbero, cobro en efectivo, hardware POS, administracion local, CRUD local de barberos, reportes locales y base para sincronizacion futura.

Este documento no autoriza crear codigo funcional, base de datos ni pantallas por si solo. Define limites tecnicos para la implementacion posterior.

## Alcance Implementable

Cuando se apruebe implementar Fase 1, el alcance tecnico sera:

- Aplicacion Windows local con C#/.NET y WinUI 3.
- Persistencia local SQLite como primera escritura de la operacion.
- Motor de asignacion de turnos en `Barberia.Core`.
- Kiosco touch para check-in de walk-ins.
- Pantalla publica de espera con turnos, barberos y reservas protegidas.
- Panel de barbero para estado operativo, escaneo de ticket e inicio de atencion.
- Autocaja operada por el barbero para cierre en efectivo.
- Abstracciones de impresora, escaner QR y cash drawer.
- Reportes locales basicos para operacion, pagos y comisiones.
- Administracion local con CRUD de barberos, imagen opcional, orden de rotacion y bandera `is_active`.
- Cola de eventos local para sincronizacion futura no bloqueante.
- Cliente API futuro aislado de la operacion local.

## Exclusiones

Fase 1 no debe implementar:

- Booking web publico.
- App movil iOS/Android.
- Cuenta web o movil de cliente.
- CRUD web de barberos.
- Panel admin web.
- Pagos online o depositos.
- Pago presencial con tarjeta.
- Seleccion de servicio en kiosco.
- Precio sugerido en autocaja.
- Migraciones cloud reales.
- Supabase Auth en la aplicacion local.
- Dependencia obligatoria de internet para operar.

Las citas pertenecen a Fase 2, pero Fase 1 debe reservar espacio tecnico para recibirlas por sincronizacion y proteger barberos por cita proxima.

El CRUD local de barberos de Fase 1 no reemplaza el CRUD web futuro: crea el contrato operativo minimo que Fase 2/3 deben sincronizar con la nube. `BarberState` representa disponibilidad operativa; `is_active` representa habilitacion administrativa para kiosco, booking y nuevos turnos.

## Arquitectura Offline-First

La aplicacion local debe escribir primero en SQLite. La base local es la autoridad para operaciones en vivo de Fase 1: check-in, asignacion, cambios de estado, escaneo, cierre, pagos en efectivo, impresion, cash drawer y reportes locales.

La sincronizacion cloud debe ser eventual y no bloqueante:

- Si no hay internet, la operacion local continua.
- Si la API cloud falla, se registra el evento local y se reintenta despues.
- Ningun modulo visual debe depender de una respuesta cloud para completar un flujo local.
- La cola de eventos debe capturar cambios relevantes para reportes, backup y futuras integraciones.
- Los conflictos de sincronizacion se documentaran antes de implementar reglas complejas.

## Limites Entre Proyectos

- `Barberia.Core` contiene reglas de negocio puras, estados, entidades/valores de dominio y motor de turnos.
- `Barberia.Core` no depende de WinUI, SQLite, EF Core, Supabase, hardware, APIs ni librerias de infraestructura.
- `Barberia.Data` maneja SQLite, repositorios, transacciones y persistencia local.
- `Barberia.Desktop` contiene WinUI 3, navegacion, composicion visual y wiring de modulos.
- `Barberia.Hardware` contiene interfaces/adaptadores para impresora, escaner QR y cash drawer.
- `Barberia.Sync` contiene cola de eventos, reintentos y coordinacion de sincronizacion futura.
- `Barberia.ApiClient` contiene comunicacion cloud futura y nunca debe bloquear operacion local.
- `Barberia.Shared` solo contiene contratos compartibles si hay una necesidad real entre proyectos.

Los estados de barbero y turno deben vivir en Core o Shared segun corresponda, no duplicados como strings o enums locales en UI.

## Riesgos Tecnicos

- Mezclar reglas de negocio en pantallas WinUI y dificultar pruebas.
- Acoplar Core a SQLite, EF Core, Supabase, hardware o API.
- Implementar booking web o app movil antes de cerrar la operacion local.
- Crear una sincronizacion que bloquee check-in, asignacion o autocaja.
- Duplicar estados entre UI, Data y Sync.
- Subestimar errores de hardware POS durante impresion o apertura de cash drawer.
- No probar el motor de turnos antes de construir UI.
- Convertir citas de Fase 2 en una funcionalidad completa dentro de Fase 1.

## Criterios Para No Mezclar Fase 2 Ni Fase 3

Una tarea pertenece a Fase 1 solo si:

- Funciona localmente en Windows.
- Puede operar sin internet.
- Es necesaria para walk-ins, barberos, turnos, autocaja, hardware POS, reportes locales o sync foundation.
- No requiere una experiencia web publica ni movil.
- No requiere cuentas de usuario cliente.
- No requiere pagos online, depositos ni autenticacion cloud.

Una tarea debe moverse a Fase 2 si implementa booking web, panel admin web, disponibilidad remota, cuentas web o administracion cloud.

Una tarea debe moverse a Fase 3 si implementa app movil, experiencia iOS/Android o flujos nativos moviles.

