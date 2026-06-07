# Flujo De Autocaja

## Reglas Confirmadas

- En Fase 1 el pago es solo en efectivo.
- En Fase 1 no hay pago presencial con tarjeta.
- El barbero selecciona el servicio prestado; el precio base es predefinido por administracion local.
- El precio base del servicio debe ser mayor que cero.
- El barbero puede seleccionar un adicional opcional de $2, $3 o $5 al precio base.
- Solo se permite un adicional por cierre de autocaja.
- El cliente paga directamente al barbero.
- El barbero cierra el servicio en autocaja.
- El panel del barbero no debe tener boton obligatorio de terminar servicio.
- El cierre operativo del servicio ocurre en autocaja.

## Flujo Confirmado

1. El barbero atiende al cliente y recibe el pago en efectivo.
2. El barbero va a autocaja con el dinero y el ticket.
3. El barbero escanea el ticket.
4. El sistema valida que el ticket pertenece al barbero autenticado que esta cerrando el servicio.
5. El barbero selecciona el servicio prestado.
6. El sistema carga el precio base configurado por administracion local.
7. El barbero puede seleccionar un adicional unico de $2, $3 o $5.
8. El sistema registra el pago con `service_id`, precio base, adicional y monto final.
9. El sistema calcula la comision.
10. El sistema imprime constancia con servicio, precio base, adicional y monto final.
11. Se abre el cash drawer.
12. El barbero deposita el efectivo.
13. El turno queda `completed`.
14. El barbero vuelve automaticamente a `available`.
15. El barbero pasa al final de la cola rotativa.

## Datos Minimos Auditables

- Barbero autenticado.
- Ticket.
- Servicio seleccionado.
- Precio base del servicio.
- Adicional seleccionado, si aplica.
- Monto final cobrado.
- Fecha y hora.
- Dispositivo.
- Constancia impresa.
- Evento de apertura de cash drawer.
- Comision calculada.

## Pendiente De Confirmar

- POR CONFIRMAR: porcentaje de comision.
- POR CONFIRMAR: manejo de propinas fuera de los adicionales configurados.
- POR CONFIRMAR: una autocaja o varias autocajas.
- POR CONFIRMAR: datos finales impresos en la constancia.
- POR CONFIRMAR: comportamiento ante error de impresora o cash drawer.
