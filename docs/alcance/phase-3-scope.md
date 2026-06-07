# Alcance Fase 3

## Resumen

Aplicacion movil iOS/Android.

## Incluye

- POR CONFIRMAR: reservas.
- POR CONFIRMAR: login.
- POR CONFIRMAR: historial de tickets (debe aprovechar los tiempos de ciclo de vida: creacion, `started_at`, `completed_at`, `cancelled_at`).
- POR CONFIRMAR: cuenta de barbero.
- POR CONFIRMAR: panel admin movil.
- POR CONFIRMAR: CRUD de barberos desde movil.
- POR CONFIRMAR: lectura del catalogo de servicios de Fase 1/2 para mostrar servicios y precios en reservas.
- Si se implementa CRUD movil de servicios, debe sincronizar nombre, precio base mayor que cero, activo/inactivo y orden de visualizacion.
- Si se implementa CRUD movil, debe usar el mismo modelo maestro de Fase 2: nombre visible, `station_number`/`station_code`, orden de rotacion, imagen de perfil opcional y `is_active`.
- Un barbero con `is_active=false` no debe aparecer para nuevas reservas ni nuevos turnos, pero su historial debe seguir visible para reportes y auditoria.
- La app movil no debe reasignar una estacion `B-#` ocupada por otro barbero activo.

## Pendiente

- TODO: confirmar tecnologia movil.
- TODO: confirmar si la app movil solo lee servicios o tambien permite administrarlos.
- TODO: confirmar permisos para que un barbero edite su propia imagen/perfil o solo administradores.
- TODO: confirmar como se notifican citas futuras cuando un barbero se desactiva desde movil.
