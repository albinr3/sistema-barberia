-- Run this after deploying the appointment-emails Edge Function and setting secrets.
-- Replace the placeholders before executing in the Supabase SQL editor.

create extension if not exists pg_cron with schema extensions;
create extension if not exists pg_net with schema extensions;

-- Store the full function URL and internal secret in Supabase Vault.
-- Example function URL:
-- https://your-project-ref.supabase.co/functions/v1/appointment-emails
select vault.create_secret(
  'https://ivfodjulouwblbpraqeu.supabase.co/functions/v1/appointment-emails',
  'appointment_email_function_url'
);

select vault.create_secret(
  'A2717EECD2ADBC9D61176162DC9A17874B6570DFA228EE62D0F79D3C46D1B030',
  'appointment_email_internal_secret'
);

-- Remove any previous job before recreating it.
select cron.unschedule('appointment-email-jobs-every-minute')
where exists (
  select 1
  from cron.job
  where jobname = 'appointment-email-jobs-every-minute'
);

select cron.schedule(
  'appointment-email-jobs-every-minute',
  '* * * * *',
  $$
  select net.http_post(
    url := (
      select decrypted_secret
      from vault.decrypted_secrets
      where name = 'appointment_email_function_url'
    ),
    headers := jsonb_build_object(
      'Content-Type', 'application/json',
      'Authorization', 'Bearer ' || (
        select decrypted_secret
        from vault.decrypted_secrets
        where name = 'appointment_email_internal_secret'
      )
    ),
    body := '{"limit":10}'::jsonb
  ) as request_id;
  $$
);
