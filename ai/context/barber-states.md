# Estados De Barbero

## Estados Iniciales Confirmados

- `not_checked_in`: el barbero no ha llegado o no ha iniciado su jornada.
- `available`: disponible para recibir turno walk-in o iniciar atencion valida.
- `called`: tiene un turno asignado o llamado, pero aun no inicio servicio.
- `in_service`: esta atendiendo un usuario.
- `offline`: termino su jornada, salio, esta en break o fue desactivado por el administrador para el dia.

## Estados Del Turno

- `waiting`: usuario hizo check-in y espera asignacion.
- `assigned`: el sistema asigno el turno a un barbero.
- `called`: el turno fue mostrado en pantalla para que el usuario pase con el barbero.
- `in_service`: el barbero escaneo el ticket y esta atendiendo al usuario.
- `completed`: turno cobrado, comprobante impreso, cash drawer abierto y servicio cerrado.
- `cancelled`: turno cancelado por personal autorizado.
- `no_show`: usuario llamado o cliente con cita no aparecio segun la regla definida.
- `voided`: turno anulado por error administrativo.

## Transiciones Confirmadas

- Solo barberos en `available` pueden recibir nuevos walk-ins.
- El barbero pasa a `in_service` cuando escanea el ticket asignado para iniciar atencion.
- Al escanear el ticket asignado, el turno tambien pasa a `in_service`.
- El panel del barbero no debe requerir boton obligatorio de terminar servicio.
- El cierre ocurre en autocaja.
- Al completar el cierre en autocaja, el turno pasa a `completed` y el barbero vuelve a `available`.
- Despues del cierre en autocaja, el barbero pasa al final de la cola rotativa.
- Si una cita confirmada entra en ventana de proteccion, el barbero queda fuera de walk-ins durante esa ventana.
- Si la cita pasa a `no_show`, se cancela o se reprograma, el barbero vuelve a `available` si no hay otra exclusion aplicable.

## Pendiente De Confirmar

- POR CONFIRMAR: si `offline` se separara en break, fin de jornada y desactivado administrativo en estados distintos.
- POR CONFIRMAR: politica de barbero que llega tarde.
- POR CONFIRMAR: permisos exactos para que barberos cambien su disponibilidad.

