# Reglas De Asignacion De Turnos

## Reglas Confirmadas

- El motor toma el usuario mas antiguo en estado `waiting`.
- Si el usuario selecciona un barbero especifico, solo ese barbero puede recibir el turno.
- Si el usuario selecciona varios barberos, solo esos barberos pueden recibir el turno.
- Si el usuario selecciona "cualquiera", cualquier barbero activo, compatible y disponible puede recibir el turno.
- `activo` significa `is_active=true`; un barbero inactivo queda fuera de kiosco, booking y nuevos turnos aunque conserve historial.
- Un barbero activo debe tener `station_number` positivo y visible como `B-#`; esta estacion es fisica y no participa en la prioridad de asignacion.
- Solo barberos en estado `available` pueden recibir nuevos walk-ins.
- Los barberos en `not_checked_in`, `called`, `in_service` u `offline` quedan fuera de la asignacion automatica.
- El motor debe excluir temporalmente a barberos protegidos por una cita confirmada proxima.

## Prioridad De Barberos Con 0 Clientes

- El sistema debe dar prioridad especial solo a barberos compatibles con 0 clientes atendidos ese dia.
- Si varios barberos compatibles tienen 0 clientes atendidos, se usa orden de llegada o cola inicial del dia.
- El sistema no debe favorecer automaticamente al barbero con menos clientes totales, salvo que tenga 0 clientes atendidos ese dia.

## Cola Rotativa

- Cuando todos los barberos compatibles ya atendieron al menos 1 cliente ese dia, se usa cola rotativa.
- Despues de cerrar servicio en autocaja, el barbero pasa al final de la cola rotativa.
- Un barbero con menos clientes totales no recibe prioridad automatica si ya atendio al menos 1 cliente ese dia.
- La cola rotativa usa `rotation_order`; no debe ordenarse por `station_number`.

## Estados Permitidos

Estados iniciales del barbero:

- `not_checked_in`
- `available`
- `called`
- `in_service`
- `offline`

Estados del turno:

- `waiting`
- `called`
- `in_service`
- `completed`
- `cancelled`
- `no_show`
- `voided`

## Flujo De Asignacion

1. Tomar el turno mas antiguo en `waiting`.
2. Determinar barberos compatibles segun seleccion del usuario.
3. Filtrar barberos con `is_active=true`.
4. Filtrar barberos compatibles en `available`.
5. Excluir barberos protegidos por cita confirmada dentro de los proximos 15 minutos.
6. Si hay compatibles con 0 clientes atendidos ese dia, asignar por orden de llegada o cola inicial.
7. Si todos los compatibles ya atendieron al menos 1 cliente, asignar por cola rotativa.
8. Cambiar el turno a `called`.
9. Cuando el barbero escanee el ticket asignado, cambiar turno y barbero a `in_service`.
10. Cuando cierre en autocaja, cambiar turno a `completed`, barbero a `available` y mover al barbero al final de la cola rotativa.
11. Si administracion local cancela un ticket activo asignado, cambiar turno a `cancelled`, devolver el barbero activo a `available` e intentar asignar automaticamente el siguiente ticket `waiting` compatible.

## Exclusiones

- Barbero no disponible por estado distinto de `available`.
- Barbero con `is_active=false`.
- Barbero no compatible con la seleccion del usuario.
- Barbero protegido por cita confirmada dentro de la ventana de 15 minutos.
- Barbero `offline` por administrador o por decision operativa.

## Pendiente De Confirmar

- POR CONFIRMAR: politica de barbero que llega tarde.
- POR CONFIRMAR: regla exacta de orden inicial del dia cuando varios barberos tienen 0 clientes.
