# Alcance Fase 1

## Resumen

Aplicacion Windows local para operacion inicial de la barberia.

## Incluye

- Turnos automaticos locales para walk-ins.
- Kiosco local touch para check-in, nombre opcional del cliente, seleccion de barberos activos sin mostrar estado textual ni indicador visual de disponibilidad y ticket impreso con numero visible diario (`1`, `2`, `3`...) y payload QR basado en el ticket interno unico.
- Pantalla publica local para sala de espera.
- Panel local de barbero para disponibilidad, tickets asignados e inicio de atencion.
- Autocaja local para cierre en efectivo operada por el barbero.
- Reportes administrativos locales, comisiones persistidas, hardware POS con simuladores y foundation de sincronizacion.
- Panel de administracion local para estado operativo, cola activa, cancelacion de tickets activos, auditoria y base SQLite.
- CRUD local de barberos con nombre, estacion fija `station_number` visible como `B-#`, orden de rotacion, imagen de perfil opcional importada desde Explorador de Windows y bandera `is_active`.
- Desactivacion local de barberos: un barbero inactivo no aparece en kiosco ni en flujos operativos locales; queda visible en administracion para reactivacion e historial, y su estacion queda disponible para otro barbero.

## Fuera De Alcance Por Ahora

- Booking web publico.
- Panel admin web.
- App movil.
- API/backend cloud real.
- Migraciones cloud reales.
- Pagos online o depositos.
- Eliminacion fisica de barberos con historial operativo; se debe preferir `is_active=false`.
