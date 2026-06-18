begin;

select plan(10);

create or replace function public.is_admin_or_owner() returns boolean language sql as $$ select true; $$;

insert into auth.users (id) values
  ('00000000-0000-0000-0000-000000000001'),
  ('00000000-0000-0000-0000-000000000002');

insert into public.profiles (id, role, display_name) values
  ('00000000-0000-0000-0000-000000000001', 'customer', 'Test Customer'),
  ('00000000-0000-0000-0000-000000000002', 'admin', 'Test Admin');

insert into public.barbers (id, display_name, station_code) values
  ('10000000-0000-0000-0000-000000000001', 'Test Barber', 'B-99');

insert into public.services (id, name, base_price_cents, duration_minutes) values
  ('20000000-0000-0000-0000-000000000001', 'Test Cut', 2500, 30);

insert into public.appointments (
  id, customer_id, barber_id, service_id, starts_at, ends_at, status, appointment_code
) values (
  '30000000-0000-0000-0000-000000000001',
  '00000000-0000-0000-0000-000000000001',
  '10000000-0000-0000-0000-000000000001',
  '20000000-0000-0000-0000-000000000001',
  now() + interval '2 hours',
  now() + interval '2 hours 30 minutes',
  'confirmed',
  'A111111111111'
);

select is(
  (select count(*)::int from public.appointment_email_jobs where appointment_id = '30000000-0000-0000-0000-000000000001'),
  2,
  'Creating an appointment enqueues confirmation and reminder emails'
);

select is(
  (select count(*)::int from public.appointment_email_jobs where appointment_id = '30000000-0000-0000-0000-000000000001' and email_type = 'created' and status = 'pending'),
  1,
  'Created email is pending'
);

select is(
  (select count(*)::int from public.appointment_email_jobs where appointment_id = '30000000-0000-0000-0000-000000000001' and email_type = 'reminder_1h' and status = 'pending'),
  1,
  'Reminder email is pending'
);

create or replace function auth.uid() returns uuid language sql as $$ select '00000000-0000-0000-0000-000000000001'::uuid; $$;

select lives_ok(
  $$ select public.cancel_customer_appointment('30000000-0000-0000-0000-000000000001', 'Customer conflict') $$,
  'Customer can cancel own appointment'
);

select is(
  (select count(*)::int from public.appointment_email_jobs where appointment_id = '30000000-0000-0000-0000-000000000001' and email_type = 'cancelled' and status = 'pending'),
  1,
  'Cancelling an appointment enqueues cancellation email'
);

select is(
  (select count(*)::int from public.appointment_email_jobs where appointment_id = '30000000-0000-0000-0000-000000000001' and email_type = 'reminder_1h' and status = 'cancelled'),
  1,
  'Cancelling an appointment cancels pending reminder'
);

select is(
  (select cancelled_by_role::text from public.appointments where id = '30000000-0000-0000-0000-000000000001'),
  'customer',
  'Customer cancellation records cancellation actor role'
);

insert into public.appointments (
  id, customer_id, barber_id, service_id, starts_at, ends_at, status, appointment_code
) values (
  '30000000-0000-0000-0000-000000000002',
  '00000000-0000-0000-0000-000000000001',
  '10000000-0000-0000-0000-000000000001',
  '20000000-0000-0000-0000-000000000001',
  now() - interval '20 minutes',
  now() + interval '10 minutes',
  'confirmed',
  'A222222222222'
);

update public.appointments
set status = 'no_show',
    no_show_at = now()
where id = '30000000-0000-0000-0000-000000000002';

select is(
  (select count(*)::int from public.appointment_email_jobs where appointment_id = '30000000-0000-0000-0000-000000000002' and email_type = 'no_show' and status = 'pending'),
  1,
  'No-show transition enqueues missed appointment email'
);

insert into public.appointments (
  id, customer_id, barber_id, service_id, starts_at, ends_at, status, appointment_code
) values (
  '30000000-0000-0000-0000-000000000003',
  '00000000-0000-0000-0000-000000000001',
  '10000000-0000-0000-0000-000000000001',
  '20000000-0000-0000-0000-000000000001',
  now() + interval '3 hours',
  now() + interval '3 hours 30 minutes',
  'confirmed',
  'A333333333333'
);

update public.appointments
set starts_at = starts_at + interval '1 hour',
    ends_at = ends_at + interval '1 hour'
where id = '30000000-0000-0000-0000-000000000003';

select is(
  (select count(*)::int from public.appointment_email_jobs where appointment_id = '30000000-0000-0000-0000-000000000003' and email_type = 'reminder_1h' and status = 'cancelled'),
  1,
  'Rescheduling cancels old reminder'
);

select is(
  (select count(*)::int from public.appointment_email_jobs where appointment_id = '30000000-0000-0000-0000-000000000003' and email_type = 'reminder_1h' and status = 'pending'),
  1,
  'Rescheduling creates new reminder'
);

select * from finish();
rollback;
