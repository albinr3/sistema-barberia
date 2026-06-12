# Barberia.Core

Libreria de dominio puro para reglas de negocio de Fase 1.

Responsabilidades:

- Motor de asignacion de turnos.
- Estados centrales de barbero y turno.
- Porcentaje de comision por barbero, con valor inicial 65%.
- Reglas de disponibilidad local, estacion fija `B-#` y cola diaria por llegada.
- Modelos de dominio sin dependencias de infraestructura.

Restricciones actuales:

- No depende de WinUI, SQLite, EF Core, Supabase, hardware ni APIs.
- El motor evalua turnos `waiting` por orden de llegada y asigna el primero que tenga barbero compatible disponible.
- Un turno especifico sin barbero compatible disponible queda esperando y no bloquea la asignacion de un turno posterior que acepte cualquier barbero.
- La cola recibida por el motor se forma por llegada diaria; los barberos sin clientes atendidos hoy siguen teniendo prioridad sobre quienes ya atendieron al menos uno.
- Despues del primer servicio de cada barbero, el cierre en autocaja mueve al barbero al final de la cola diaria.
- La fecha operativa que construye esa cola se calcula en la capa desktop con horario de New Jersey/Eastern Time antes de llamar al dominio.
