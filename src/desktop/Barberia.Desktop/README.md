# Barberia.Desktop

Aplicacion Windows local de Fase 1 con WinUI 3.

Responsabilidades futuras:

- Navegacion y composicion visual.
- Wiring de dependencias entre modulos.
- Pantallas de kiosco, pantalla publica, panel de barbero, autocaja, administracion local y reportes.

Shell actual:

- Entrada WinUI con `App.xaml`, `App`, `MainWindow` y navegacion lateral simple.
- Catalogo de modulos visuales en `Shell/ShellModuleCatalog.cs`.
- Paginas placeholder separadas por responsabilidad visual en `Views/`, con estilo base para la shell.
- Rutas locales estables en `Services/LocalAppPaths.cs` para preservar SQLite, configuracion futura y logs durante updates.
- Perfil de publicacion `Properties/PublishProfiles/Phase1LocalWinX64.pubxml` y artefactos base en `Packaging/` para preparar instalacion/update sin publicar instaladores.

Restricciones actuales:

- No contiene flujos operativos completos.
- No contiene reglas de negocio.
- No contiene persistencia ni acceso directo a hardware o servicios cloud.
- No publica MSIX, MSI, EXE ni App Installer sin aprobacion humana.
