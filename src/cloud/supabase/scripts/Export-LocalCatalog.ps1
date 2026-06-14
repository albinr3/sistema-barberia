param (
    [string]$DbPath = "$env:LOCALAPPDATA\BarberiaSystem\barberia-local.db",
    [string]$OutputPath = "$PSScriptRoot\import_catalog.sql"
)

if (-not (Test-Path $DbPath)) {
    Write-Host "No se encontró la base de datos local en: $DbPath" -ForegroundColor Red
    exit 1
}

Write-Host "Leyendo datos de $DbPath..." -ForegroundColor Cyan

$query = @"
.mode list
.separator "|||"

SELECT 'INSERT INTO public.barbers (id, display_name, station_code, is_active) VALUES (''' || id || ''', ''' || REPLACE(display_name, '''', '''''') || ''', ' || CASE WHEN station_number IS NULL THEN 'NULL' ELSE '''B-' || station_number || '''' END || ', ' || CASE WHEN is_active = 1 THEN 'true' ELSE 'false' END || ') ON CONFLICT(id) DO NOTHING;'
FROM barbers;

SELECT 'INSERT INTO public.services (id, name, base_price_cents, sort_order, is_active) VALUES (''' || id || ''', ''' || REPLACE(name, '''', '''''') || ''', ' || price_cents || ', ' || display_order || ', ' || CASE WHEN is_active = 1 THEN 'true' ELSE 'false' END || ') ON CONFLICT(id) DO NOTHING;'
FROM services;
"@

$sqlCommands = $query | sqlite3 $DbPath

$outputContent = @"
-- Auto-generated catalog export from SQLite
-- Source: $DbPath
-- Generado el: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

-- ==========================================
-- BARBERS
-- ==========================================
$($sqlCommands | Where-Object { $_ -match "^INSERT INTO public\.barbers" } | Out-String)

-- ==========================================
-- SERVICES
-- ==========================================
$($sqlCommands | Where-Object { $_ -match "^INSERT INTO public\.services" } | Out-String)
"@

Set-Content -Path $OutputPath -Value $outputContent -Encoding UTF8

Write-Host "Exportación completada exitosamente. Archivo generado en:" -ForegroundColor Green
Write-Host $OutputPath -ForegroundColor Yellow
Write-Host "`nPuedes copiar el contenido de este archivo y ejecutarlo en el SQL Editor de Supabase."
