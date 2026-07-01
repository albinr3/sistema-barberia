# Instalación por estaciones

## 1. Preparar PC3

PC3 será el host de operaciones. Es la única máquina que debe abrir la base SQLite real y la única que debe sincronizar con Supabase.

### 1.1 Red fija para PC3

PC3 debe tener una IP estable en la red local para que PC1 y PC2 puedan conectarse siempre al mismo servidor LAN. En esta guía se usa `192.168.1.50` como ejemplo.

Recomendado: crear una reserva DHCP en el router para la MAC de PC3. Alternativa: configurar IP fija manual en Windows.

Antes de escoger la IP, verifica la red actual desde PC3:

```powershell
ipconfig
```

Si la puerta de enlace es `192.168.1.1`, una IP como `192.168.1.50` normalmente pertenece a la misma red. Antes de usarla, valida que no esté ocupada:

```powershell
ping 192.168.1.50
```

Si responde y PC3 todavía no tiene esa IP, usa otra IP libre y actualiza `lanServerUrl` en PC1 y PC2.

### 1.2 Evitar suspensión

En PC3, desactiva suspensión/sleep para que el host LAN no se apague mientras PC1/PC2 están operando.

Ruta típica en Windows:

```text
Configuración > Sistema > Energía y suspensión
```

Configura suspensión en `Nunca` cuando esté conectado a corriente.

### 1.3 Abrir firewall para TCP 5128

Ejecuta PowerShell como Administrador en PC3 y crea la regla de entrada:

```powershell
New-NetFirewallRule -DisplayName "Barberia Operations LAN TCP 5128" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5128
```

Para confirmar que la regla existe:

```powershell
Get-NetFirewallRule -DisplayName "Barberia Operations LAN TCP 5128"
```

### 1.4 Crear carpetas locales de datos

En PC3, crea la carpeta de configuración si aún no existe:

```powershell
New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\BarberiaSystem\config"
```

La ruta completa normalmente queda así:

```text
C:\Users\TU_USUARIO\AppData\Local\BarberiaSystem\
```

### 1.5 Ubicar `sync-settings.json`

Solo PC3 debe tener este archivo:

```text
%LocalAppData%\BarberiaSystem\config\sync-settings.json
```

Ejemplo de estructura:

```json
{
  "supabaseUrl": "https://TU-PROYECTO.supabase.co",
  "deviceId": "PC3-OPERATIONS",
  "deviceSecret": "CAMBIA-ESTE-SECRETO-SUPABASE",
  "pollSeconds": 30
}
```

Notas:

- No copies `sync-settings.json` a PC1 ni PC2.
- `pollSeconds` debe ser `30` o mayor; la app fuerza mínimo `30`.
- Este secreto es de sincronización cloud. No tiene que ser igual al `lanSharedSecret` usado entre las PCs.

### 1.6 Ubicar `barberia-local.db`

Solo PC3 debe tener la base real:

```text
%LocalAppData%\BarberiaSystem\barberia-local.db
```

Si estás moviendo una base existente:

1. Cierra `Barberia.Operations`, `Barberia.KioskRotation` y `Barberia.CashBox`.
2. Copia `barberia-local.db` a `%LocalAppData%\BarberiaSystem\barberia-local.db` en PC3.
3. No pongas la base dentro de la carpeta publicada de la app.
4. No compartas la SQLite por carpeta de red.
5. No copies la base real a PC1 ni PC2.

## 2. Compilar/publicar los 3 sistemas

Estos pasos se ejecutan en la máquina donde tienes el repo y el SDK de .NET instalado.

### 2.1 Prerrequisitos

Desde la raíz del repo, confirma que `dotnet` está disponible:

```powershell
dotnet --info
```

Si el repo viene recién descargado o cambiaste dependencias, restaura paquetes:

```powershell
dotnet restore BarberiaSystem.sln
```

### 2.2 Publicar PC1, PC2 y PC3

Desde la raíz del repo:

```powershell
dotnet publish src\desktop\Barberia.Desktop\Barberia.Desktop.csproj --no-restore -p:PublishProfile=KioskRotationWinX64 -v:minimal
dotnet publish src\desktop\Barberia.Desktop\Barberia.Desktop.csproj --no-restore -p:PublishProfile=CashBoxWinX64 -v:minimal
dotnet publish src\desktop\Barberia.Desktop\Barberia.Desktop.csproj --no-restore -p:PublishProfile=OperationsWinX64 -v:minimal
```

Qué genera cada perfil:

- `KioskRotationWinX64`: app para PC1, kiosko y rotación de barberos.
- `CashBoxWinX64`: app para PC2, caja, recibos y gaveta.
- `OperationsWinX64`: app para PC3, host LAN, base local y sync cloud.

### 2.3 Salidas generadas

Después de publicar, copia cada carpeta a su PC correspondiente:

- PC1: `src\desktop\Barberia.Desktop\bin\Release\kiosk-rotation-win-x64\`
- PC2: `src\desktop\Barberia.Desktop\bin\Release\cashbox-win-x64\`
- PC3: `src\desktop\Barberia.Desktop\bin\Release\operations-win-x64\`

Dentro de cada carpeta publicada debe estar el ejecutable de esa estación, por ejemplo:

- PC1: `Barberia.KioskRotation.exe`
- PC2: `Barberia.CashBox.exe`
- PC3: `Barberia.Operations.exe`

### 2.4 Reglas importantes al copiar

- Copia la carpeta completa de salida, no solo el `.exe`.
- Mantén cada perfil en su PC correspondiente.
- No copies `barberia-local.db` junto con la app publicada.
- No copies `sync-settings.json` junto con la app publicada.
- Los datos locales van en `%LocalAppData%\BarberiaSystem`, no en la carpeta de instalación/publicación.

## 3. Crear configuración por PC

Ruta en cada máquina:

```text
%LocalAppData%\BarberiaSystem\config\station-settings.json
```

Crea la carpeta si no existe:

```powershell
New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\BarberiaSystem\config"
```

### PC3

```json
{
  "role": "OperationsHost",
  "lanServerUrl": "http://localhost:5128",
  "lanListenUrl": "http://0.0.0.0:5128",
  "deviceId": "PC3-OPERATIONS",
  "lanSharedSecret": "CAMBIA-ESTE-SECRETO",
  "startLanHostInDevelopment": false
}
```

### PC1

```json
{
  "role": 3,
  "lanServerUrl": "http://192.168.1.50:5128",
  "lanListenUrl": "http://0.0.0.0:5128",
  "deviceId": "PC1-KIOSK",
  "lanSharedSecret": "CAMBIA-ESTE-SECRETO",
  "startLanHostInDevelopment": false
}
```

### PC2

```json
{
  "role": "CashBox",
  "lanServerUrl": "http://192.168.1.50:5128",
  "lanListenUrl": "http://0.0.0.0:5128",
  "deviceId": "PC2-CASHBOX",
  "lanSharedSecret": "CAMBIA-ESTE-SECRETO",
  "startLanHostInDevelopment": false
}
```

El `lanSharedSecret` debe ser igual en las 3 PCs.

Si cambiaste la IP de PC3, reemplaza `192.168.1.50` por la IP real en PC1 y PC2.

## 4. Orden de encendido

1. Encender PC3.
2. Abrir `Barberia.Operations`.
3. Confirmar desde PC1/PC2 que responde:

   ```powershell
   Invoke-RestMethod http://192.168.1.50:5128/health
   ```

4. Abrir PC1.
5. Abrir PC2.

Si PC3 no responde, PC1/PC2 deben bloquear operación.

## 5. Hardware

- PC1: impresora de kiosko como impresora predeterminada de Windows; PC1 registra el ticket en PC3 por LAN y luego imprime el ticket localmente.
- PC2: impresora de recibos/reporte como predeterminada de Windows.
- Gaveta en PC2 según el adaptador real disponible; ahora el flujo LAN deja la ejecución de hardware del Cash Box del lado de PC2.
- PC3 no necesita impresoras para operar la base/API; no debe depender de impresora para aceptar tickets de kiosko remotos.
- Si PC1 no puede imprimir, el ticket puede quedar registrado en PC3; corrige la impresora de PC1 antes de crear otro ticket.

## 6. Validación operativa

Prueba en este orden:

1. PC3 abre Ticket Dashboard + Appointments.
2. PC1 abre Kiosk + Barber Rotation.
3. PC1 crea ticket en kiosko.
4. PC1 imprime ticket.
5. PC1 hace check-in de barbero en rotation.
6. PC3 dashboard refleja ticket/estado.
7. PC3 o Appointments inicia cita/ticket si aplica.
8. PC2 abre caja con opening cash.
9. PC2 busca ticket.
10. PC2 cierra servicio.
11. PC2 imprime recibo y abre gaveta.
12. PC3 dashboard/reportes reflejan cobro.
13. Supabase recibe sync desde PC3.

## 7. Updates remotos

Para MSIX/App Installer todavía falta reemplazar en las plantillas:

- dominio real de updates
- certificado real
- identidad MSIX real
- versión real

Plantillas agregadas en:

```text
src\desktop\Barberia.Desktop\Packaging\
```

Las 3 apps deben publicarse siempre con la misma versión.

