# Packaging De Barberia.Desktop

Esta carpeta contiene artefactos de preparacion para instalar y actualizar la app Windows local de Fase 1. No contiene instaladores publicados.

## Artefactos

- `../Properties/PublishProfiles/Phase1LocalWinX64.pubxml`: perfil de publicacion local `win-x64`, self-contained, para preparar una carpeta instalable o validar hardware sin depender del runtime instalado en la maquina.
- `barberia-system.appinstaller.template`: plantilla de App Installer para la ruta MSIX aprobada. Debe reemplazarse con dominio de updates, certificado y version reales antes de publicar.

## Comandos De Preparacion

```powershell
dotnet publish src/desktop/Barberia.Desktop/Barberia.Desktop.csproj --no-restore -p:PublishProfile=Phase1LocalWinX64 -v:minimal
```

La salida queda en `src/desktop/Barberia.Desktop/bin/Release/phase1-local-win-x64/`.

## Politica De Publicacion

- No publicar `.msix`, `.msixbundle`, `.appinstaller`, MSI ni EXE sin aprobacion humana.
- La ruta preferida de actualizaciones es MSIX + App Installer si las pruebas con impresora POS, escaner QR y cash drawer no muestran bloqueos.
- Si MSIX bloquea hardware o permisos, usar el perfil self-contained como entrada para un MSI/EXE clasico aprobado.
- Los datos locales se mantienen fuera de la carpeta de instalacion en `%LocalAppData%\BarberiaSystem`.

## Publicacion por estaciones

La division LAN usa el mismo codigo base con tres perfiles de publicacion. El rol se infiere por nombre de ejecutable y tambien puede forzarse por `station-settings.json` o `--station`.

```powershell
dotnet publish src/desktop/Barberia.Desktop/Barberia.Desktop.csproj --no-restore -p:PublishProfile=KioskRotationWinX64 -v:minimal
dotnet publish src/desktop/Barberia.Desktop/Barberia.Desktop.csproj --no-restore -p:PublishProfile=CashBoxWinX64 -v:minimal
dotnet publish src/desktop/Barberia.Desktop/Barberia.Desktop.csproj --no-restore -p:PublishProfile=OperationsWinX64 -v:minimal
```

Salidas esperadas:

- `bin/Release/kiosk-rotation-win-x64/` para PC1.
- `bin/Release/cashbox-win-x64/` para PC2.
- `bin/Release/operations-win-x64/` para PC3.

Plantillas MSIX/App Installer separadas:

- `barberia-kiosk-rotation.appinstaller.template`
- `barberia-cashbox.appinstaller.template`
- `barberia-operations.appinstaller.template`

Antes de publicar, reemplazar dominio, certificado, identidad MSIX y version real. Las tres apps deben publicarse con la misma version para mantener compatible `LanApiContract.Version`.

## Operacion LAN

- PC3 (`Barberia.Operations`) debe estar encendida antes de abrir PC1 o PC2.
- PC1/PC2 bloquean la operacion si `/health` no responde o si la version LAN no coincide.
- No compartir `barberia-local.db` por carpeta de red; solo PC3 abre SQLite.
- Configurar IP fija/reserva DHCP para PC3, firewall para el puerto definido en `lanListenUrl`, suspension desactivada y preferiblemente Ethernet/UPS.
