# Barberia.Data

Libreria de persistencia local SQLite para la operacion offline-first de Fase 1.

Responsabilidades:

- Inicializar el esquema SQLite local.
- Persistir barberos, turnos, citas sincronizadas minimas, pagos en efectivo y eventos auditables.
- Exponer repositorios y transacciones para guardar decisiones tomadas por `Barberia.Core` o por flujos coordinados desde la aplicacion.

Restricciones:

- Depende de `Barberia.Core`.
- No usa EF Core.
- No depende de WinUI, Supabase, cloud ni hardware.
- No decide reglas de asignacion ni cambios de estado por su cuenta.
