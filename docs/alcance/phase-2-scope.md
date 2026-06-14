# Alcance Fase 2

## Resumen

Componentes web para login/registro, booking autenticado, usuarios, panel de barbero, administracion y backend cloud con Supabase.

## Incluye

- Booking web autenticado detras de login.
- Cuenta de usuario cliente.
- Panel admin web para admin/owner.
- Panel web de barbero.
- Next.js + TypeScript para web.
- Supabase Auth/PostgreSQL/RLS para backend cloud.
- CRUD web de barberos sincronizado con el CRUD local de Fase 1.
- CRUD web de servicios sincronizado con el catalogo local de Fase 1: nombre, precio base mayor que cero, activo/inactivo y orden de visualizacion.
- Sincronizacion del historial de tickets, incluyendo los tiempos de ciclo de vida completos (`started_at`, `completed_at`, `cancelled_at`), desde la operacion local hacia Supabase/PostgreSQL.
- El booking web debe mostrar los servicios disponibles con su precio base.
- Los barberos cloud deben incluir al menos `id`, nombre visible, `station_number`/`station_code`, orden de rotacion, imagen de perfil opcional, estado operativo sincronizable cuando aplique y bandera `is_active`.
- La bandera `is_active=false` debe ocultar al barbero de booking web, kiosco local y flujos operativos para nuevos turnos, sin borrar historial.
- La estacion `B-#` debe ser unica entre barberos activos y liberarse cuando `is_active=false`.
- Disponibilidad diaria administrada en Supabase.

## Pendiente

- TODO: instalar dependencias web y validar build/lint.
- TODO: crear o vincular proyecto Supabase real.
- TODO: definir reglas de conflicto cuando web/cloud y Windows local editen el mismo barbero sin conexion.
- TODO: definir reglas de conflicto cuando web/cloud y Windows local editen el mismo servicio sin conexion.
- TODO: definir almacenamiento cloud de imagenes de barbero y sincronizacion/cache local hacia `Assets` o carpeta administrada.
- TODO: confirmar regla para citas futuras ya confirmadas cuando un barbero se desactiva desde web.
- TODO: definir contrato POS/sync antes de crear tablas cloud definitivas para tickets y ticket_items.
- TODO: depositos online quedan fuera del MVP inicial y pasan a subfase posterior con Stripe/webhooks.
