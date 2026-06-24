param (
    [string]$DbPath = "$env:LOCALAPPDATA\BarberiaSystem\barberia-local.db"
)

if (-not (Test-Path $DbPath)) {
    Write-Host "No se encontró la base de datos local en: $DbPath" -ForegroundColor Red
    exit 1
}

Write-Host "Limpiando datos operativos locales, conservando catálogo y configuración de sincronización..." -ForegroundColor Cyan

$query = @"
PRAGMA foreign_keys = OFF;

-- Nómina
DELETE FROM payroll_payment_items;
DELETE FROM payroll_lines;
DELETE FROM payroll_adjustments;
DELETE FROM payroll_pending_adjustments;
DELETE FROM payroll_periods;

-- Operación y Ventas
DELETE FROM cash_payments;
DELETE FROM pending_service_payments;
DELETE FROM turns;
DELETE FROM appointment_reservations;
DELETE FROM barber_daily_rotation;
DELETE FROM daily_operation_state;

-- Eventos de Sincronización (Outbox) y Auditoría
DELETE FROM audit_events;
DELETE FROM sync_outbox_events;

-- Limpieza y optimización
VACUUM;

PRAGMA foreign_keys = ON;
"@

$query | sqlite3 $DbPath

Write-Host "¡Limpieza completada! Base de datos local lista para empezar de cero." -ForegroundColor Green
