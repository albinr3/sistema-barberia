# Barberia.Core

Libreria de dominio puro para reglas de negocio de Fase 1.

Responsabilidades futuras:

- Motor de asignacion de turnos.
- Estados centrales de barbero y turno.
- Reglas de disponibilidad local, estacion fija `B-#` y cola rotativa.
- Modelos de dominio sin dependencias de infraestructura.

Restricciones actuales:

- No depende de WinUI, SQLite, EF Core, Supabase, hardware ni APIs.
- No implementa el motor de turnos todavia.
- No contiene logica de negocio funcional en este skeleton.
