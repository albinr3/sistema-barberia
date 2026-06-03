# Reglas De Citas

## Fase

- Las citas pertenecen a Fase 2.
- Booking web y app movil no deben implementarse como funcionalidades de Fase 1.
- La Fase 1 debe estar preparada para recibir citas mediante sincronizacion cuando exista Fase 2.

## Reglas Confirmadas

- Las citas deben sincronizarse hacia la aplicacion Windows local.
- Una cita confirmada debe bloquear al barbero 15 minutos antes de la hora.
- Durante la ventana de proteccion de 15 minutos, el barbero no debe recibir walk-ins.
- Cuando el cliente con cita llega, debe hacer check-in como cliente con cita.
- El sistema valida la reserva y activa un turno local asociado a esa cita.
- Una cita confirmada y llegada dentro de su ventana valida tiene prioridad sobre walk-ins para respetar el horario reservado.
- Si el cliente con cita no llega 10 minutos despues de la hora, la cita pasa a `no_show` y el barbero vuelve a `available`.
- El administrador podra reprogramar citas `no_show` desde web o app movil, segun la fase.
- La pantalla publica debe diferenciar visualmente citas programadas y turnos walk-in.
- La pantalla publica debe mostrar algo como "Reservado para cita" cuando un barbero este protegido por cita proxima.
- Las cuentas de usuario cliente para citas requieren nombre, correo electronico y contrasena.
- Las contrasenas no deben guardarse directamente en la base del negocio; deben gestionarse con Supabase Auth u otro proveedor seguro equivalente.
- Supabase/PostgreSQL sera la base cloud compartida para citas, autenticacion, disponibilidad, reportes y sincronizacion.

## Estados Relacionados

- `appointment.confirmed`: cita confirmada desde web o app movil.
- `appointment.protection_started`: inicio de ventana de proteccion antes de la cita.
- `appointment.checked_in`: cliente con cita llego y fue validado.
- `appointment.no_show`: cliente no llego dentro de la ventana definida.
- `appointment.rescheduled`: cita reprogramada por administrador.
- `appointment.cancelled`: cita cancelada.

## Pendiente De Confirmar

- POR CONFIRMAR: politica de depositos.
- POR CONFIRMAR: si el deposito es reembolsable, no reembolsable o reutilizable para reprogramacion.
- POR CONFIRMAR: si el `no_show` al minuto 10 ocurre automaticamente o requiere confirmacion manual del administrador.
- POR CONFIRMAR: reglas de cancelacion y reprogramacion permitidas para el usuario.
- POR CONFIRMAR: regla final para citas ya confirmadas cuando un barbero se desactiva o sale `offline` antes de la cita.

