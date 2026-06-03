# Barberia.Data

Libreria de persistencia local futura.

Responsabilidades futuras:

- SQLite local.
- Repositorios, mapeos y transacciones.
- Persistencia de decisiones tomadas por el dominio o flujos coordinados desde la aplicacion.

Restricciones actuales:

- Depende de `Barberia.Core`.
- No configura EF Core.
- No crea base de datos real.
- No implementa repositorios ni logica de persistencia.
