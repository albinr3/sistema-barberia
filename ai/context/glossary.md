# Glosario

## Terminos Confirmados

- Usuario: persona que llega a la barberia o reserva una cita para recortarse.
- Cliente: dueno de la barberia que contrata el desarrollo del sistema.
- Usuario cliente: cuenta usada por una persona para reservar citas web o moviles.
- Barbero: persona que atiende usuarios, recibe comision y puede operar autocaja para cerrar sus servicios.
- Estacion de barbero: puesto fisico fijo asignado a un barbero activo con codigo `B-#`, unico entre activos y liberado cuando el barbero se desactiva.
- Administrador: dueno o manager con permisos para reportes, configuracion, caja, comisiones y disponibilidad.
- Walk-in: usuario que llega sin cita y entra a la cola local.
- Turno: registro generado cuando un usuario hace check-in o cuando una cita se integra a la operacion local.
- Cita: reserva programada desde web o app movil; pertenece a Fase 2/Fase 3 y se sincroniza con Fase 1.
- Ticket: comprobante impreso con numero de turno y codigo QR; el payload QR operativo es el numero de ticket.
- Kiosco: pantalla touch de entrada para check-in.
- Pantalla publica: pantalla de espera que muestra turnos, barberos, walk-ins y citas programadas.
- Panel de barbero: modulo para check-in del barbero, cambios de estado e inicio de atencion por escaneo de ticket.
- Autocaja: modulo o punto donde el barbero escanea ticket, digita monto cobrado, imprime constancia, abre cash drawer y deposita efectivo.
- Cash drawer: caja fisica de dinero que se abre desde la impresora POS o hardware compatible.
- Constancia: comprobante impreso para el barbero con datos del monto depositado.
- Cola rotativa: orden de asignacion usado cuando todos los barberos compatibles ya atendieron al menos 1 cliente ese dia.
- Ventana de proteccion de cita: periodo de 15 minutos antes de una cita confirmada durante el cual el barbero no recibe walk-ins.
- `no_show`: estado aplicado cuando un usuario llamado o cliente con cita no aparece dentro de la regla definida.
- Supabase Auth: proveedor seguro para gestionar contrasenas de cuentas de usuario cliente, evitando guardarlas directamente en la base del negocio.

## Pendiente De Confirmar

- POR CONFIRMAR: nombres finales visibles en UI para cada estado y modulo.
- POR CONFIRMAR: idioma de interfaz: ingles, espanol o ambos.
