begin;

select plan(8);

-- Setup
create or replace function public.is_admin_or_owner() returns boolean language sql as $$ select true; $$;
insert into auth.users (id) values ('00000000-0000-0000-0000-000000000001');

-- Mock current user
create or replace function auth.uid() returns uuid language sql as $$ select '00000000-0000-0000-0000-000000000001'::uuid; $$;

insert into public.synced_tickets (id, local_ticket_id, source_device_id, status, appointment_id)
values 
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'device1', 'waiting', null),
  ('cccccccc-cccc-cccc-cccc-cccccccccccc', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'device1', 'in_progress', null),
  ('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'ffffffff-ffff-ffff-ffff-ffffffffffff', 'device1', 'waiting', '99999999-9999-9999-9999-999999999999'),
  ('11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222', 'device1', 'called', null),
  ('33333333-3333-3333-3333-333333333333', '44444444-4444-4444-4444-444444444444', 'device1', 'completed', null);

-- Test 1: Successful cancellation (waiting)
select lives_ok(
  $$ select public.admin_cancel_ticket('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa') $$,
  'Can cancel a waiting ticket'
);

-- Test 2: Successful cancellation (in_progress)
select lives_ok(
  $$ select public.admin_cancel_ticket('cccccccc-cccc-cccc-cccc-cccccccccccc') $$,
  'Can cancel an in_progress ticket'
);

-- Test 3: Successful cancellation (called)
select lives_ok(
  $$ select public.admin_cancel_ticket('11111111-1111-1111-1111-111111111111') $$,
  'Can cancel a called ticket'
);

-- Test 4: Fails for ticket not found
select throws_ok(
  $$ select public.admin_cancel_ticket('00000000-0000-0000-0000-000000000000') $$,
  'Ticket not found',
  'Cannot cancel non-existent ticket'
);

-- Test 5: Fails for ticket belonging to appointment
select throws_ok(
  $$ select public.admin_cancel_ticket('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee') $$,
  'Cannot cancel a ticket that belongs to an appointment. Modify the appointment instead.',
  'Cannot cancel appointment ticket'
);

-- Test 6: Fails for completed ticket
select throws_ok(
  $$ select public.admin_cancel_ticket('33333333-3333-3333-3333-333333333333') $$,
  'Only waiting, called, or in_progress tickets can be cancelled',
  'Cannot cancel completed ticket'
);

-- Test 7: Audit log and command were created for the first test
select is(
  (select count(*)::int from public.audit_log where action = 'admin_ticket_cancel_requested'),
  3,
  'Audit log was inserted for successful cancellations'
);

select is(
  (select count(*)::int from public.ticket_admin_commands where command_type = 'cancel' and status = 'pending'),
  3,
  'Cancel commands were created'
);

-- Test 8: Auth enforcement
create or replace function public.is_admin_or_owner() returns boolean language sql as $$ select false; $$;
select throws_ok(
  $$ select public.admin_cancel_ticket('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa') $$,
  'Unauthorized: Must be an admin or owner to cancel tickets',
  'Fails if not admin'
);

select * from finish();
rollback;
