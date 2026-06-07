# Barberia.Core

Libreria de dominio puro para reglas de negocio de Fase 1.

Responsabilidades:

- Motor de asignacion de turnos.
- Estados centrales de barbero y turno.
- Reglas de disponibilidad local, estacion fija `B-#` y cola rotativa.
- Modelos de dominio sin dependencias de infraestructura.

Restricciones actuales:

- No depende de WinUI, SQLite, EF Core, Supabase, hardware ni APIs.
- El motor evalua turnos `waiting` por orden de llegada y asigna el primero que tenga barbero compatible disponible.
- Un turno especifico sin barbero compatible disponible queda esperando y no bloquea la asignacion de un turno posterior que acepte cualquier barbero.
