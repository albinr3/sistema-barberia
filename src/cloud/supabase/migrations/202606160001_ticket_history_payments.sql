-- Migración 202606160001_ticket_history_payments.sql

-- Add new columns for ticket history and payment reference
alter table public.synced_payments
  add column if not exists receipt_number text,
  add column if not exists payment_reference text;
