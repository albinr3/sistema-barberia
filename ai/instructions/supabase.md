# Instrucciones Supabase

## Proposito

Instrucciones para trabajo relacionado con Supabase en Fase 2.

## Reglas

- Fase 2 usa Supabase Auth/PostgreSQL/RLS como backend cloud aprobado.
- No crear ni vincular un proyecto Supabase real sin aprobacion explicita.
- No ejecutar migraciones contra una base real sin aprobacion explicita.
- Las migraciones deben activar RLS desde el inicio.
- `profiles` no debe duplicar credenciales de Supabase Auth; solo datos de dominio.
- Operaciones sensibles deben ir por RPC/Edge Functions, no por acceso directo del cliente.
- Documentar cualquier decision en el decision log.

## Pendiente

- TODO: definir contrato sync desktop-cloud antes de crear tablas POS cloud definitivas.

