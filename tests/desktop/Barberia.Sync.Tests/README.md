# Barberia.Sync.Tests

Pruebas futuras para `Barberia.Sync`.

Alcance futuro:

- Cola de eventos.
- Reintentos.
- Comportamiento no bloqueante ante fallos de conectividad.

Estado actual:

- Cubre outbox SQLite local, envio exitoso, fallos cloud convertidos en reintentos y omision de eventos hasta su proximo intento.
- No configura Supabase ni sincronizacion real.
