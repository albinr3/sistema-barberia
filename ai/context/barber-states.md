# Estados De Barbero

## Estados Iniciales Confirmados

- `not_checked_in`: el barbero no ha llegado o no ha iniciado su jornada.
- `available`: disponible para recibir turno walk-in o iniciar atencion valida.
- `called`: tiene un turno asignado o llamado, pero aun no inicio servicio.
- `in_service`: esta atendiendo un usuario.
- `offline`: termino su jornada, salio o esta en break.

## Atributos Administrativos Del Barbero

- `is_active=true`: el barbero esta habilitado para aparecer en kiosco, pantalla publica y flujos operativos locales.
- `is_active=false`: el barbero esta desactivado administrativamente; no recibe nuevos walk-ins ni debe aparecer para nuevas reservas, pero conserva historial, reportes y auditoria.
- `station_number`: numero positivo de estacion fisica para barberos activos. Se muestra como `B-#`, debe ser unico entre barberos activos y se libera al desactivar el barbero.
- `profile_image_path`: imagen opcional de perfil. En Fase 1 puede apuntar a `Assets/...` o a `ProfileImages/...` despues de importar desde Explorador de Windows. Si no existe, las pantallas deben usar placeholder con iniciales.

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
- Solo barberos con `is_active=true` pueden aparecer en kiosco o recibir nuevos walk-ins.
- El barbero pasa a `in_service` cuando escanea el ticket asignado para iniciar atencion.
- Al escanear el ticket asignado, el turno tambien pasa a `in_service`.
- El panel del barbero no debe requerir boton obligatorio de terminar servicio.
- El cierre ocurre en autocaja.
- Al completar el cierre en autocaja, el turno pasa a `completed` y el barbero vuelve a `available`.
- Despues del cierre en autocaja, el barbero pasa al final de la cola rotativa.
- Si una cita confirmada entra en ventana de proteccion, el barbero queda fuera de walk-ins durante esa ventana.
- Si la cita pasa a `no_show`, se cancela o se reprograma, el barbero vuelve a `available` si no hay otra exclusion aplicable.
- Al desactivar un barbero desde administracion local, si no esta `called` ni `in_service`, queda `offline` y `is_active=false`.
- Al desactivar un barbero, su `station_number` queda vacio para que esa estacion pueda asignarse a otro barbero.
- Un barbero `called` o `in_service` no puede desactivarse hasta resolver el turno.

## Pendiente De Confirmar

- POR CONFIRMAR: si `offline` se separara en break y fin de jornada en estados distintos.
- POR CONFIRMAR: politica de barbero que llega tarde.
- POR CONFIRMAR: permisos exactos para que barberos cambien su disponibilidad.
