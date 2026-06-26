# Barberia.Desktop

Aplicacion Windows local de Fase 1 con WinUI 3.

Responsabilidades:

- Navegacion y composicion visual.
- Wiring de dependencias entre modulos.
- Pantallas de kiosco, pantalla publica, check-in local de barberos por estacion, panel de barbero con validacion de estacion antes de iniciar tickets, autocaja, administracion local, reportes y nomina semanal.
- Administracion local separada en paginas dedicadas (`LocalAdminPage` para el dashboard general, `BarbersPage` para el CRUD de barberos, y `ServicesPage` para el CRUD de servicios) compartiendo la misma logica del `LocalAdminService`; `BarbersPage` uses a full-page roster table with create/edit operations in a modal editor, preserving the same local admin save, delete, image import, commission percentage, and availability actions; `ServicesPage` usa chrome de pantalla completa sin panel lateral, tabla de catalogo y modal para crear/editar servicios sin exponer el editor fijo anterior; `LocalAdminPage` usa chrome de pantalla completa sin panel lateral y muestra KPI, alertas, monitor de cola, historial del dia, auditoria, roster, reasignacion de tickets y accesos internos a barberos/servicios. Permite reasignar tickets `waiting` o `called` a barberos activos, llamandolos solo si el destino esta disponible y ya hizo check-in en la rotacion diaria; si esta ocupado, offline o sin check-in, el ticket queda `waiting` reservado para ese barbero. Autocaja usa el catalogo de servicios y el porcentaje de comision del barbero para calcular el monto sin campo libre y muestra los servicios activos como botones de una sola pulsacion en una grilla compacta de tres columnas.
- `PayrollPage` usa chrome de pantalla completa como Cash Box/Local Admin y se conecta a `PayrollService` para generar vistas previas de semanas viernes-jueves, bloquear cambios sobre periodos pagados y consultar historial. `PayrollAutoPayService` se inicia con la app y marca automaticamente como pagado el ultimo periodo cerrado desde el viernes 12:00am New Jersey/Eastern Time; si la app estaba cerrada, lo aplica al volver a abrir. No existe pago manual ni ajustes manuales en la superficie operativa.
- Calculo en memoria de alertas administrativas para notificar problemas operativos en base a umbrales de tiempo.

Shell y UI actual:

- Entrada WinUI con `App.xaml`, `App.xaml.cs`, `MainWindow.xaml` y `MainWindow.xaml.cs`.
- Toda nueva `Window` o `Page` concreta debe crearse como par `.xaml` + `.xaml.cs`, con `partial` e `InitializeComponent()`.
- Recursos visuales compartidos en `Styles/DesktopTheme.xaml`, mezclados desde `App.xaml`.
- Catalogo de modulos visuales en `Shell/ShellModuleCatalog.cs`, incluyendo `BarberCheckInPage` para registrar la llegada por estacion y construir la cola diaria.
- Paginas operativas en `Views/` declaradas en XAML y con logica de servicio en code-behind.
- Rutas locales estables en `Services/LocalAppPaths.cs` para preservar SQLite, configuracion futura y logs durante updates.
- Sincronizacion cloud opcional mediante `%LocalAppData%\BarberiaSystem\config\sync-settings.json`; si falta o es invalida, la operacion local sigue funcionando sin bloquearse.
- Perfil de publicacion `Properties/PublishProfiles/Phase1LocalWinX64.pubxml` y artefactos base en `Packaging/` para preparar instalacion/update sin publicar instaladores.
- Guard de arquitectura en `tests/desktop/Barberia.Desktop.Tests` para evitar nuevas pantallas C# puras sin XAML.

Sync cloud:

```json
{
  "supabaseUrl": "https://your-project-ref.supabase.co",
  "deviceId": "00000000-0000-0000-0000-000000000000",
  "deviceSecret": "replace-with-device-secret",
  "pollSeconds": 30
}
```

- En cada ciclo, `DesktopSyncService` descarga y aplica cambios cloud antes de encolar snapshots locales de barberos/servicios y nomina. Los snapshots se comparan por contenido operativo/catalogo y no por `updated_at`, para evitar loops donde un eco de Supabase vuelva a generar eventos cada 60 segundos.
- Los comandos administrativos web, como `ticket.reassign`, se aplican cuando `DesktopSyncService` hace pull de `sync-changes`; `PublicDisplayPage` vuelve a leer SQLite local cada 5 segundos mientras esta visible. Con `pollSeconds` en 30, el Ticket Dashboard local debe reflejar reasignaciones web sin navegar fuera de la pagina en una ventana normal de 30 a 60 segundos.
- No hay Realtime/socket directo entre Web y Desktop en esta fase; Desktop sigue siendo offline-first y la pantalla local solo redibuja el estado que ya fue aplicado en SQLite.
- Payroll web usa `snapshot_requested` para pedir recalculo, pero no paga nomina ni crea ajustes manuales. Desktop es la unica autoridad de pago: recalcula contra SQLite, marca el periodo cerrado como pagado automaticamente y confirma con `payroll.snapshot`. Comandos legacy `payroll.adjustment_added` se responden como fallidos.
- Desktop emite `desktop.sync_heartbeat` con `pending_outbox_count`; web lo usa solo para estado operativo/sync. El boton `Pay Payroll` ya no existe en web.
- Las citas sincronizadas se guardan en `appointment_reservations` con `appointment_code`, cliente, servicio, hora de inicio/fin y estado local.
- `AppointmentsPage` muestra las citas del dia como una lista operativa directa; al refrescar aplica localmente las citas vencidas a `NoShow` antes de renderizar, las filas priorizan citas activas y conservan el QR/codigo visible para operacion. Tambien incluye un micro panel visible de escaneo fisico que exige estacion de barbero y luego QR/codigo, mantiene foco en estacion y reutiliza `BarberPanelService.StartService(...)` para iniciar servicios desde QR de cita o tickets llamados sin cambiar de pantalla.
- Barber Public y Local Admin controlan si un barbero activo aparece como seleccionable en el kiosko, pero no crean la rotacion diaria. `BarberCheckInPage` exige digitar/escanear la estacion (`B-#`, `b-#` o numero) y crea la entrada en `barber_daily_rotation`; al hacer check-in intenta llamar el proximo ticket `waiting` compatible.
- El kiosko permite imprimir tickets para barberos seleccionables aunque todavia no hayan hecho check-in. Esos tickets quedan `waiting` hasta que exista al menos un barbero compatible con entrada de rotacion del dia operativo New Jersey/Eastern.
- En `PublicDisplayPage`, la seccion `Barber Status` muestra arriba los barberos disponibles que todavia tienen `0` clientes atendidos hoy; si hay varios con `0`, conserva el orden de la cola diaria por llegada. Despues muestra los disponibles que ya atendieron clientes, tambien conservando la cola diaria.
- Barber Panel exige escanear/digitar primero la estacion fija del barbero (`B-#`) y luego el ticket o QR. Solo inicia un ticket si la estacion coincide con el barbero asignado; para citas acepta el QR `appointment_code` dentro de la ventana de 15 minutos antes a 10 minutos despues, valida que la estacion coincida con el barbero de la cita, crea un turno `Appointment`, lo marca `InService` y sube `appointment.checked_in`.
- Cash Box completa la cita al cerrar el cobro inmediato o al usar `Pay Later`; en ambos casos el servicio/turno queda completado y el barbero vuelve a `Available`. Si el barbero tenia check-in del dia, autocaja lo mueve al final de la cola; si no tenia entrada de rotacion, no crea una implicitamente. `Pay Later` crea `pending_service_payments` y sube `ticket.completed`/`appointment.completed`, pero no crea `cash_payments`, no imprime recibo final, no abre gaveta y no cuenta para nomina hasta cobrar.
- Cash Box muestra `Pending Payments (N)` para cobrar uno o varios servicios pendientes del dia operativo New Jersey/Eastern. El modal es touch-first, exige ingresar la estacion del barbero que cobra como dato historico (`Collected by`) y `Collect Selected` crea un `cash_payments` por ticket con el mismo recibo/metodo/referencia del cobro grupal. El recibo grupal lista los tickets cobrados y sus barberos originales; el barbero cobrador no recibe comision ni reasignacion de dinero por ese campo.
- Appointment turns are operational records for Cash Box/Barber Panel, but are not ticket-dashboard rows.
- Cash Box contiene un botón `Reprint Receipts` oculto bajo contraseña (`G1234`) para abrir la ventana de reimpresión `ReceiptReprintWindow`; el listado muestra ticket/recibo, barbero, servicio, adicional, total y acción de reimpresión. Los fallos de la impresora o la gaveta durante el cierre de venta no cancelan la transacción, sino que se auditan como `cash_box_hardware_failure` y se indica el fallo en pantalla.

Backups y restore:

- `BackupsPage` configura backups automaticos diarios en hora `America/New_York`, permite ejecutar backup manual y lista los ZIP locales guardados en `%LocalAppData%\BarberiaSystem\backups`.
- La restauracion v1 cubre ZIP locales listados y ZIP externos elegidos con `Restore from file`; no lista ni descarga backups desde Supabase.
- Antes de restaurar, Desktop valida que el ZIP contenga exactamente una base `.db`, extrae con la contraseña guardada o una contraseña ingresada manualmente, ejecuta `PRAGMA integrity_check`, inicializa/migra el esquema local y valida tablas minimas.
- Restore se bloquea si la DB actual o la DB restaurada tiene eventos pendientes en `sync_outbox_events`; el operador debe esperar a que sincronice antes de restaurar para evitar perder o reenviar cambios locales.
- Antes de reemplazar `barberia-local.db`, Desktop crea un ZIP de seguridad `pre-restore_yyyyMMdd_HHmmss.zip` en la carpeta local de backups.
- Restore crea un evento `desktop.restore_applied` dentro de la DB restaurada. Al reiniciar, Web adopta el snapshot restaurado de tickets/cobros y marca como revertidos los tickets/items/pagos que existian en Web pero no estan en el backup restaurado.
- Web no borra registros posteriores al backup: los conserva como auditoria con `restore_reverted_at`, pero dashboards, ventas, ticket history activo y breakdowns de nomina filtran esos registros revertidos.
- Restore no intenta operar en vivo: al terminar, cerrar y abrir Barberia Desktop antes de seguir operando para que el sync envie el restore autoritativo a Web.
Restricciones actuales:

- No contiene reglas de negocio.
- No mover persistencia, hardware ni sincronizacion al code-behind de UI.
- No publica MSIX, MSI, EXE ni App Installer sin aprobacion humana.
