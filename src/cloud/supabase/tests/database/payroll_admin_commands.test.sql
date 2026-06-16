begin;

select plan(7);

create or replace function public.is_admin_or_owner() returns boolean language sql as $$ select true; $$;
insert into auth.users (id) values ('00000000-0000-0000-0000-000000000001');
create or replace function auth.uid() returns uuid language sql as $$ select '00000000-0000-0000-0000-000000000001'::uuid; $$;

insert into public.sync_devices (id, name, device_secret_hash, is_active, last_sync_at, pending_outbox_count)
values ('11111111-1111-1111-1111-111111111111', 'Front desk', 'secret', true, now(), 0);

insert into public.barbers (id, display_name, is_active)
values ('22222222-2222-2222-2222-222222222222', 'Ana', true);

select lives_ok(
  $$ select public.admin_request_payroll_snapshot('11111111-1111-1111-1111-111111111111', '2026-06-05', '2026-06-12') $$,
  'Can request a payroll snapshot'
);

select throws_ok(
  $$ select public.admin_add_payroll_adjustment('11111111-1111-1111-1111-111111111111', '2026-06-05', '2026-06-12', '22222222-2222-2222-2222-222222222222', 500, 'Bonus') $$,
  'A payroll command is already pending for this period',
  'Prevents two pending payroll commands for the same period'
);

update public.payroll_admin_commands set status = 'applied';

select lives_ok(
  $$ select public.admin_add_payroll_adjustment('11111111-1111-1111-1111-111111111111', '2026-06-05', '2026-06-12', '22222222-2222-2222-2222-222222222222', 500, 'Bonus') $$,
  'Can request a payroll adjustment'
);

update public.payroll_admin_commands set status = 'applied';
update public.sync_devices set last_sync_at = now() - interval '20 minutes';

select throws_ok(
  $$ select public.admin_request_payroll_payment('11111111-1111-1111-1111-111111111111', '2026-06-05', '2026-06-12', 'cash', 'WEB-1', null) $$,
  'Desktop sync is stale. Payroll payment cannot be requested yet',
  'Blocks payment when desktop sync is stale'
);

update public.sync_devices set last_sync_at = now(), pending_outbox_count = 2;

select throws_ok(
  $$ select public.admin_request_payroll_payment('11111111-1111-1111-1111-111111111111', '2026-06-05', '2026-06-12', 'cash', 'WEB-1', null) $$,
  'Desktop has pending sync events. Payroll payment cannot be requested yet',
  'Blocks payment when desktop has pending outbox events'
);

update public.sync_devices set pending_outbox_count = 0;

select throws_ok(
  $$ select public.admin_request_payroll_payment('11111111-1111-1111-1111-111111111111', current_date, current_date + 7, 'cash', 'WEB-1', null) $$,
  'Payroll period has not closed yet',
  'Blocks payment for an open payroll period'
);

insert into public.synced_payroll_periods (
  source_device_id,
  local_period_id,
  start_date,
  end_date,
  state,
  total_services,
  total_commission_cents,
  total_adjustments_cents,
  total_to_pay_cents,
  generated_at,
  loaded_at
) values (
  '11111111-1111-1111-1111-111111111111',
  '33333333-3333-3333-3333-333333333333',
  '2026-06-05',
  '2026-06-12',
  'draft',
  1,
  1000,
  0,
  1000,
  now(),
  now()
);

select lives_ok(
  $$ select public.admin_request_payroll_payment('11111111-1111-1111-1111-111111111111', '2026-06-05', '2026-06-12', 'cash', 'WEB-1', null) $$,
  'Can request payment for a closed clean synced draft period'
);

select * from finish();
rollback;
