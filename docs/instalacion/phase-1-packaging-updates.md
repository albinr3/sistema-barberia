# Packaging Y Actualizaciones De Fase 1

## Decision

La ruta preferida para Fase 1 es empaquetar `Barberia.Desktop` como MSIX y distribuirlo con App Installer, sin Microsoft Store, despues de validar hardware POS real o equivalente. Esta ruta permite instalacion formal y updates remotos, pero no debe publicarse ningun instalador sin aprobacion humana.

Mientras no exista aprobacion de publicacion y certificado, el repo solo deja preparados:

- Versionado base en `src/desktop/Barberia.Desktop/Barberia.Desktop.csproj`.
- Perfil local `Phase1LocalWinX64.pubxml` para generar una salida self-contained de prueba.
- Plantilla `Packaging/barberia-system.appinstaller.template` para definir updates MSIX cuando existan dominio, certificado y paquete real.

## Datos Locales Durante Updates

La carpeta de instalacion debe tratarse como reemplazable. La app no debe guardar datos operativos ahi.

Rutas locales estables:

- Base SQLite: `%LocalAppData%\BarberiaSystem\barberia-local.db`.
- Configuracion futura: `%LocalAppData%\BarberiaSystem\config`.
- Imagenes importadas de barberos: `%LocalAppData%\BarberiaSystem\profile-images`.
- Logs: `%LocalAppData%\BarberiaSystem\logs`.

Reglas:

- Un update nunca debe borrar `%LocalAppData%\BarberiaSystem`.
- La base SQLite existente debe mantenerse en la misma ruta para no romper instalaciones previas.
- Las imagenes importadas de barberos deben conservarse entre updates y no empaquetarse como datos reemplazables.
- Cambios futuros de esquema deben ser migraciones locales aditivas y probadas antes de publicar.
- Antes de un update con cambio de esquema, preparar backup de `barberia-local.db`.
- Logs y configuracion local no deben empaquetarse dentro del instalador.

## Estrategia De Update

La plantilla App Installer usa:

- Revision en launch cada 12 horas.
- Prompt visible al usuario si hay update.
- `UpdateBlocksActivation=false` para no impedir que la barberia opere si el update no puede aplicarse.
- Background task de Windows para revisiones periodicas.
- Sin `ForceUpdateFromAnyVersion` para evitar downgrades accidentales.

Si la PC no tiene internet, la app instalada sigue operando con SQLite local. El update queda pendiente hasta que vuelva la conexion o hasta que se aplique manualmente.

## Fallback MSI/EXE

Si MSIX/App Installer no es compatible con hardware POS, permisos, impresora, cash drawer o politicas del equipo, usar un instalador clasico MSI/EXE aprobado. En ese caso:

- La salida self-contained de `Phase1LocalWinX64.pubxml` es la base del instalador.
- El instalador debe conservar `%LocalAppData%\BarberiaSystem`.
- El update debe reemplazar binarios, no datos locales.
- Debe incluir rollback operativo documentado antes de usarlo con cliente.

## Checklist Antes De Publicar

- Confirmar certificado de firma y `Publisher` definitivo.
- Confirmar dominio o recurso seguro para hosting de updates.
- Generar paquete versionado y revisar que la version sube en formato `major.minor.build.revision`.
- Instalar en una maquina limpia.
- Validar check-in, pantalla publica, panel de barbero, autocaja y reportes sin internet.
- Validar impresora, escaner QR y cash drawer.
- Ejecutar un update de prueba y verificar que `barberia-local.db` queda intacta.
- Ejecutar `dotnet test BarberiaSystem.sln --no-restore -m:1 -v:minimal`.
