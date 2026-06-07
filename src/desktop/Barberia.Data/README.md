# Barberia.Data

Libreria de persistencia local SQLite para la operacion offline-first de Fase 1.

Responsabilidades:

- Inicializar el esquema SQLite local.
- Persistir barberos con `station_number`, servicios con precio base, turnos, citas sincronizadas minimas, pagos en efectivo y eventos auditables.
- Guardar en `cash_payments` el servicio cobrado, precio base, adicional y monto final para historial y reportes.
- Exponer repositorios y transacciones para guardar decisiones tomadas por `Barberia.Core` o por flujos coordinados desde la aplicacion.

Restricciones:

- Depende de `Barberia.Core`.
- No usa EF Core.
- No depende de WinUI, Supabase, cloud ni hardware.
- No decide reglas de asignacion ni cambios de estado por su cuenta.
