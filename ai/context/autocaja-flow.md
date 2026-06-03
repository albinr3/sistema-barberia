# Flujo De Autocaja

## Reglas Confirmadas

- En Fase 1 el pago es solo en efectivo.
- En Fase 1 no hay pago presencial con tarjeta.
- En Fase 1 no hay precio sugerido en autocaja.
- El cliente paga directamente al barbero.
- El barbero cierra el servicio en autocaja.
- El panel del barbero no debe tener boton obligatorio de terminar servicio.
- El cierre operativo del servicio ocurre en autocaja.

## Flujo Confirmado

1. El barbero atiende al cliente y recibe el pago en efectivo.
2. El barbero va a autocaja con el dinero y el ticket.
3. El barbero escanea el ticket.
4. El sistema valida que el ticket pertenece al barbero autenticado que esta cerrando el servicio.
5. El barbero digita el monto cobrado en efectivo.
6. El sistema registra el pago.
7. El sistema calcula la comision.
8. El sistema imprime constancia del monto depositado.
9. Se abre el cash drawer.
10. El barbero deposita el efectivo.
11. El turno queda `completed`.
12. El barbero vuelve automaticamente a `available`.
13. El barbero pasa al final de la cola rotativa.

## Datos Minimos Auditables

- Barbero autenticado.
- Ticket.
- Monto cobrado.
- Fecha y hora.
- Dispositivo.
- Constancia impresa.
- Evento de apertura de cash drawer.
- Comision calculada.

## Pendiente De Confirmar

- POR CONFIRMAR: porcentaje de comision.
- POR CONFIRMAR: manejo de propinas.
- POR CONFIRMAR: una autocaja o varias autocajas.
- POR CONFIRMAR: datos finales impresos en la constancia.
- POR CONFIRMAR: comportamiento ante error de impresora o cash drawer.

