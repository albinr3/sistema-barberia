# Barberia.Desktop

Aplicacion Windows local de Fase 1 con WinUI 3.

Responsabilidades:

- Navegacion y composicion visual.
- Wiring de dependencias entre modulos.
- Pantallas de kiosco, pantalla publica, panel de barbero, autocaja, administracion local, reportes y nomina semanal.
- Administracion local separada en paginas dedicadas (`LocalAdminPage` para el dashboard general, `BarbersPage` para el CRUD de barberos, y `ServicesPage` para el CRUD de servicios) compartiendo la misma logica del `LocalAdminService`; `BarbersPage` uses a full-page roster table with create/edit operations in a modal editor, preserving the same local admin save, delete, image import, commission percentage, and availability actions; `ServicesPage` usa chrome de pantalla completa sin panel lateral, tabla de catalogo y modal para crear/editar servicios sin exponer el editor fijo anterior; `LocalAdminPage` usa chrome de pantalla completa sin panel lateral y muestra KPI, alertas, monitor de cola, historial del dia, auditoria, roster, reasignacion de tickets y accesos internos a barberos/servicios. Permite reasignar tickets `waiting` o `called` a barberos activos, reservandolos para el destino si esta ocupado; autocaja usa el catalogo de servicios y el porcentaje de comision del barbero para calcular el monto sin campo libre y muestra los servicios activos como botones de una sola pulsacion en una grilla compacta de tres columnas.
- `PayrollPage` usa chrome de pantalla completa como Cash Box/Local Admin y se conecta a `PayrollService` para generar vistas previas en memoria de semanas viernes-jueves, aplicar ajustes manuales temporales, registrar pago semanal completo con persistencia a base de datos y bloquear cambios sobre periodos pagados.
- Calculo en memoria de alertas administrativas para notificar problemas operativos en base a umbrales de tiempo.

Shell y UI actual:

- Entrada WinUI con `App.xaml`, `App.xaml.cs`, `MainWindow.xaml` y `MainWindow.xaml.cs`.
- Toda nueva `Window` o `Page` concreta debe crearse como par `.xaml` + `.xaml.cs`, con `partial` e `InitializeComponent()`.
- Recursos visuales compartidos en `Styles/DesktopTheme.xaml`, mezclados desde `App.xaml`.
- Catalogo de modulos visuales en `Shell/ShellModuleCatalog.cs`.
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
  "pollSeconds": 60
}
```

- En cada ciclo, `DesktopSyncService` descarga y aplica cambios cloud antes de encolar el snapshot local de barberos/servicios. El snapshot se compara por contenido operativo/catalogo y no por `updated_at`, para evitar loops donde un eco de Supabase vuelva a generar `catalog.snapshot` cada 60 segundos.
- Las citas sincronizadas se guardan en `appointment_reservations` con `appointment_code`, cliente, servicio, hora de inicio/fin y estado local.
- `AppointmentsPage` muestra las citas del dia como una lista operativa directa; las filas priorizan citas activas y conservan el QR/codigo visible para operacion.
- Barber Panel acepta el QR `appointment_code` dentro de la ventana de 15 minutos antes a 10 minutos despues, crea un turno `Appointment`, lo marca `InService` y sube `appointment.checked_in`.
- Cash Box completa la cita solo al cerrar el cobro; ahi se marcan el turno y la reserva como completados y se suben `ticket.completed`, `payment.collected` y `appointment.completed`.
- Appointment turns are operational records for Cash Box/Barber Panel, but are not ticket-dashboard rows.
Restricciones actuales:

- No contiene reglas de negocio.
- No mover persistencia, hardware ni sincronizacion al code-behind de UI.
- No publica MSIX, MSI, EXE ni App Installer sin aprobacion humana.
