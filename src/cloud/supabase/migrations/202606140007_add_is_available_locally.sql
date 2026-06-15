-- Add is_available_locally column to barbers table
alter table public.barbers add column is_available_locally boolean not null default true;
