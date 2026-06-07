# Barberia.Desktop

Aplicacion Windows local de Fase 1 con WinUI 3.

Responsabilidades:

- Navegacion y composicion visual.
- Wiring de dependencias entre modulos.
- Pantallas de kiosco, pantalla publica, panel de barbero, autocaja, administracion local y reportes.
- Administracion local de barberos y servicios; autocaja usa el catalogo de servicios para calcular el monto sin campo libre.

Shell y UI actual:

- Entrada WinUI con `App.xaml`, `App.xaml.cs`, `MainWindow.xaml` y `MainWindow.xaml.cs`.
- Toda nueva `Window` o `Page` concreta debe crearse como par `.xaml` + `.xaml.cs`, con `partial` e `InitializeComponent()`.
- Recursos visuales compartidos en `Styles/DesktopTheme.xaml`, mezclados desde `App.xaml`.
- Catalogo de modulos visuales en `Shell/ShellModuleCatalog.cs`.
- Paginas operativas en `Views/` declaradas en XAML y con logica de servicio en code-behind.
- Rutas locales estables en `Services/LocalAppPaths.cs` para preservar SQLite, configuracion futura y logs durante updates.
- Perfil de publicacion `Properties/PublishProfiles/Phase1LocalWinX64.pubxml` y artefactos base en `Packaging/` para preparar instalacion/update sin publicar instaladores.
- Guard de arquitectura en `tests/desktop/Barberia.Desktop.Tests` para evitar nuevas pantallas C# puras sin XAML.

Restricciones actuales:

- No contiene reglas de negocio.
- No mover persistencia, hardware ni sincronizacion al code-behind de UI.
- No publica MSIX, MSI, EXE ni App Installer sin aprobacion humana.
