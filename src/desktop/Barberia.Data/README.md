# Barberia.Data

Libreria de persistencia local SQLite para la operacion offline-first de Fase 1.

# Barberia.Data

Libreria de persistencia local SQLite para la operacion offline-first de Fase 1.

Responsabilidades:

- Inicializar el esquema SQLite local.
- Persistir barberos con `station_number`, `commission_percentage`, servicios con precio base, turnos, citas sincronizadas minimas, pagos en efectivo y eventos auditables.
- Persistir el reset operativo diario y la cola diaria de barberos en `daily_operation_state` y `barber_daily_rotation`; `barbers.rotation_order` queda como columna legacy sin controlar asignacion.
- Los timestamps y fechas operativas que recibe Data vienen normalizados por Desktop al horario de New Jersey/Eastern Time.
- Guardar en `cash_payments` el servicio cobrado, precio base, adicional y monto final para historial y reportes.
- Crear `barbers.commission_percentage` con valor por defecto 65 para nuevas bases y backfill local de barberos existentes.
- Persistir nomina semanal local de viernes a jueves con `payroll_periods`, `payroll_lines` y `payroll_payment_items`; los indices unicos evitan duplicar rangos de nomina y volver a incluir un pago ya marcado como pagado. Las tablas legacy `payroll_adjustments` y `payroll_pending_adjustments` pueden existir en bases de desarrollo, pero no deben usarse para nuevas funcionalidades.
- La nomina usa solo `cash_payments.commission_cents` ya guardado; no recalcula comisiones historicas desde el porcentaje actual del barbero.
- No depende de WinUI, Supabase, cloud ni hardware.
- No decide reglas de asignacion ni cambios de estado por su cuenta.
