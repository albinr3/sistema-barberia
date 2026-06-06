# Reglas De Negocio

## Reglas Confirmadas

- El sistema se desarrolla por fases: Fase 1 Windows local, Fase 2 booking web y Fase 3 app movil.
- La Fase 1 es una aplicacion Windows local con C#/.NET/WinUI 3 y SQLite local.
- La Fase 1 debe ser offline-first: la barberia debe operar aunque no haya internet.
- La operacion local de Fase 1 debe escribir primero en SQLite.
- La nube no debe bloquear la operacion local.
- Supabase/PostgreSQL sera la base cloud compartida para booking web, app movil, autenticacion, disponibilidad, citas, reportes y sincronizacion.
- Booking web y app movil no son funcionalidades implementables de Fase 1.
- En Fase 1 no hay seleccion de servicio en kiosco.
- En Fase 1 no hay precio sugerido en autocaja.
- En Fase 1 no hay pago presencial con tarjeta.
- En Fase 1 el pago es solo en efectivo y el cliente paga directamente al barbero.
- El barbero cierra el servicio en autocaja.
- El panel del barbero no debe tener un boton obligatorio de terminar servicio; el cierre operativo ocurre en autocaja.
- Solo barberos en estado `available` pueden recibir nuevos walk-ins.
- Un barbero tambien debe tener `is_active=true` para aparecer en kiosco, pantalla publica y flujos operativos locales.
- Todo barbero activo debe tener una estacion fisica fija con codigo visible `B-#`, por ejemplo `B-1`.
- La estacion fisica debe ser unica entre barberos activos.
- Al desactivar un barbero, su estacion se libera y puede asignarse a otro barbero.
- La estacion fisica no reemplaza ni modifica el `rotation_order`; el `rotation_order` sigue siendo la cola de asignacion.
- Desactivar un barbero (`is_active=false`) lo oculta de nuevos turnos y reservas futuras sin borrar su historial.
- Al desactivar un barbero desde administracion local, si no esta `called` ni `in_service`, su estado operativo local pasa a `offline`.
- No se debe eliminar fisicamente un barbero con historial operativo; se debe desactivar para mantener reportes, auditoria y sincronizacion futura.
- La administracion local puede cancelar tickets activos (`waiting`, `called`, `in_service`); si el ticket tenia barbero asignado, el barbero activo vuelve a `available` en la misma transaccion y se intenta asignar automaticamente el siguiente ticket `waiting` compatible.
- Las citas pertenecen a Fase 2, pero deben integrarse con Fase 1 mediante sincronizacion.
- La pantalla publica debe diferenciar visualmente citas programadas y turnos walk-in.
- La pantalla publica debe mostrar un estado como "Reservado para cita" cuando un barbero este protegido por cita proxima.
- Las cuentas de usuario cliente para citas requieren nombre, correo electronico y contrasena.
- Las contrasenas no deben guardarse directamente en la base del negocio; deben gestionarse con Supabase Auth u otro proveedor seguro equivalente.
- Los cambios administrativos hechos desde web o app movil deben guardarse en la nube y sincronizarse hacia Windows cuando haya conexion.
- La base local es autoridad para operaciones en vivo de Fase 1; Supabase recibe copia sincronizada para reportes, backup, booking web y app movil.

## Reglas Pendientes De Confirmar

- POR CONFIRMAR: politica de barbero que llega tarde.
- POR CONFIRMAR: porcentaje de comision.
- POR CONFIRMAR: una autocaja o varias autocajas.
- POR CONFIRMAR: manejo de propinas.
- POR CONFIRMAR: idioma de interfaz: ingles, espanol o ambos.
- POR CONFIRMAR: exportacion de reportes a Excel/PDF.
- POR CONFIRMAR: politica de depositos: reembolsable, no reembolsable o reutilizable para reprogramacion.
- POR CONFIRMAR: si el `no_show` de citas es automatico al minuto 10 o requiere confirmacion manual del administrador.
- POR CONFIRMAR: si el administrador remoto debe estar disponible desde Fase 1 o esperar a Fase 2.
- POR CONFIRMAR: si los barberos pueden modificar su disponibilidad libremente o requieren aprobacion.
- POR CONFIRMAR: regla final para citas confirmadas cuando un barbero se desactiva o sale `offline` antes de la cita.
- POR CONFIRMAR: quien compra, instala y valida el hardware POS.
