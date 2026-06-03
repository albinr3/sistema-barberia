# Decision Log

| fecha | decision | motivo | impacto | estado |
|---|---|---|---|---|
| 2026-06-02 | Usar monorepo por componentes tecnicos | Ordenar el trabajo por superficie tecnica | El codigo se organiza por desktop, cloud, mobile, tests y database | Confirmada |
| 2026-06-02 | Manejar fases con milestones, labels e issues | Evitar carpetas separadas por fase y mantener arquitectura por componente | Las fases no definen la estructura fisica del repo | Confirmada |
| 2026-06-02 | Fase 1 sera Windows local con C#/.NET/WinUI 3 | Buena integracion con Windows, UI moderna y hardware local | La primera implementacion sera una aplicacion desktop Windows | Confirmada |
| 2026-06-02 | SQLite local sera la base de operacion offline-first | La barberia debe operar aunque no haya internet | La app local escribe primero en SQLite y sincroniza despues | Confirmada |
| 2026-06-02 | Supabase/PostgreSQL sera base cloud compartida | Centralizar booking web, app movil, autenticacion, disponibilidad, citas, reportes y sincronizacion | La nube complementa la operacion local pero no la bloquea | Confirmada |
| 2026-06-02 | Fase 1 tendra pago en efectivo solamente | Reducir alcance y evitar pagos presenciales con tarjeta en Fase 1 | No se implementan tarjetas presenciales ni metodos mixtos en Fase 1 | Confirmada |
| 2026-06-02 | La autocaja sera operada por el barbero | El barbero cobra efectivo y cierra su servicio en autocaja | El cierre registra monto, constancia, cash drawer, comision y estado final | Confirmada |
| 2026-06-02 | Booking web pertenece a Fase 2 | Separar reservas online del sistema local inicial | Fase 1 no implementa booking web, pero debe integrarse luego por sincronizacion | Confirmada |
| 2026-06-02 | App movil pertenece a Fase 3 | Validar primero Fase 1 y booking web antes de app iOS/Android | La app movil reutilizara el backend de Fase 2 | Confirmada |
| PENDIENTE | Licencia del proyecto | Falta seleccionar licencia | No distribuir hasta confirmar | Pendiente |
