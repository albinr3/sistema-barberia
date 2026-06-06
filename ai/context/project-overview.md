# Project Overview

## Fuente Oficial

La fuente oficial inicial del proyecto es:

- `ai/context/estructura_proyecto_barberia_winui3_actualizado_v2.docx`

Todo agente debe leer este documento y estos archivos de contexto antes de proponer o implementar logica.

## Resumen Del Producto

Sistema de turnos, caja y booking para una barberia. El producto se divide en tres fases para controlar alcance, costo y riesgo tecnico.

## Fase 1: Sistema Local Windows

Objetivo: operar la barberia internamente desde check-in hasta cierre de servicio por autocaja.

Alcance confirmado:

- Aplicacion Windows local con C#/.NET/WinUI 3.
- SQLite local como base de operacion offline-first.
- Kiosco touch de check-in.
- Turnos automaticos.
- Pantalla publica de espera.
- Panel de barbero.
- Autocaja operada por el barbero.
- Administracion local con CRUD de barberos, estacion fija `B-#`, imagen opcional y desactivacion por `is_active`.
- Reportes, comisiones, hardware POS y sincronizacion cloud.
- Operacion local aunque no haya internet.

No implementar en Fase 1:

- Booking web.
- App movil.
- Seleccion de servicio en kiosco.
- Precio sugerido en autocaja.
- Pago presencial con tarjeta.

## Fase 2: Booking Web

Objetivo: permitir reservas desde web y sincronizarlas con la operacion local.

Alcance confirmado:

- Booking web publico.
- Cuenta de usuario cliente.
- Depositos online.
- Panel admin web.
- CRUD cloud/web de barberos sincronizado con el CRUD local.
- Soporte de `is_active` para ocultar barberos de booking, kiosco y nuevos turnos sin borrar historial.
- Soporte de `station_number`/`station_code` para conservar la estacion fija del barbero activo.
- Disponibilidad diaria.
- Cuenta web de barbero.
- Sincronizacion de citas y disponibilidad con Windows.

## Fase 3: App Movil

Objetivo: crear app iOS/Android reutilizando el backend de Fase 2.

Alcance confirmado:

- Reservas desde app movil.
- Registro/login.
- Historial de citas.
- Cuenta de barbero.
- Panel admin movil.
- CRUD movil de barberos si se aprueba, reutilizando el modelo cloud con `is_active`.
- CRUD movil de barberos debe reutilizar `station_number`/`station_code` si administra barberos.
- Control rapido de disponibilidad.

## Arquitectura General Confirmada

- La aplicacion local de Fase 1 debe escribir primero en SQLite.
- La nube no debe bloquear la operacion local.
- Supabase/PostgreSQL sera base cloud compartida para booking web, app movil, autenticacion, disponibilidad, citas, reportes y sincronizacion.
- Las fases se gestionan con milestones, labels e issues, no con carpetas separadas.
