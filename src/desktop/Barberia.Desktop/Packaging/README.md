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
