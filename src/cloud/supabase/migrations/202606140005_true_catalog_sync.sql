-- Remove catalog mappings and clear data for true bi-directional sync

-- 1. Drop the mapping table and its policies
drop table if exists public.desktop_catalog_mappings cascade;

-- 2. Clear out all existing catalog data and related appointments to start fresh with Desktop's source of truth
truncate table public.appointments cascade;
truncate table public.barbers cascade;
truncate table public.services cascade;
