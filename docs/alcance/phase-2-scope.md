# Alcance Fase 2

## Resumen

Componentes web para booking, usuarios y administracion.

## Incluye

- POR CONFIRMAR: booking web.
- POR CONFIRMAR: cuenta de usuario cliente.
- POR CONFIRMAR: depositos online.
- POR CONFIRMAR: panel admin web.
- CRUD web de barberos sincronizado con el CRUD local de Fase 1.
- CRUD web de servicios sincronizado con el catalogo local de Fase 1: nombre, precio base mayor que cero, activo/inactivo y orden de visualizacion.
- El booking web debe mostrar los servicios disponibles con su precio base.
- Los barberos cloud deben incluir al menos `id`, nombre visible, `station_number`/`station_code`, orden de rotacion, imagen de perfil opcional, estado operativo sincronizable cuando aplique y bandera `is_active`.
- La bandera `is_active=false` debe ocultar al barbero de booking web, kiosco local y flujos operativos para nuevos turnos, sin borrar historial.
- La estacion `B-#` debe ser unica entre barberos activos y liberarse cuando `is_active=false`.
- POR CONFIRMAR: disponibilidad diaria.
- POR CONFIRMAR: cuenta web de barbero.

## Pendiente

- TODO: confirmar stack web y backend.
- TODO: definir reglas de conflicto cuando web/cloud y Windows local editen el mismo barbero sin conexion.
- TODO: definir reglas de conflicto cuando web/cloud y Windows local editen el mismo servicio sin conexion.
- TODO: definir almacenamiento cloud de imagenes de barbero y sincronizacion/cache local hacia `Assets` o carpeta administrada.
- TODO: confirmar regla para citas futuras ya confirmadas cuando un barbero se desactiva desde web.
