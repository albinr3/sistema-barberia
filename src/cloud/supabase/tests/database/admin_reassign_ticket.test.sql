begin;

select plan(9);

-- Setup
create or replace function public.is_admin_or_owner() returns boolean language sql as $$ select true; $$;
insert into auth.users (id) values ('00000000-0000-0000-0000-000000000001');

-- Mock current user
create or replace function auth.uid() returns uuid language sql as $$ select '00000000-0000-0000-0000-000000000001'::uuid; $$;

insert into public.barbers (id, display_name, is_active, is_available_locally) 
values 
  ('11111111-1111-1111-1111-111111111111', 'Active Barber', true, true),
  ('22222222-2222-2222-2222-222222222222', 'Inactive Barber', false, true),
  ('33333333-3333-3333-3333-333333333333', 'Not Available Barber', true, false);

insert into public.synced_tickets (id, local_ticket_id, source_device_id, status, appointment_id)
values 
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'device1', 'waiting', null),
  ('cccccccc-cccc-cccc-cccc-cccccccccccc', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'device1', 'in_progress', null),
  ('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'ffffffff-ffff-ffff-ffff-ffffffffffff', 'device1', 'waiting', '99999999-9999-9999-9999-999999999999');

-- Test 1: Successful reassignment
select lives_ok(
  $$ select public.admin_reassign_ticket('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '11111111-1111-1111-1111-111111111111') $$,
  'Can reassign a waiting ticket to an active and available barber'
);

-- Test 2: Fails for ticket not found
select throws_ok(
  $$ select public.admin_reassign_ticket('00000000-0000-0000-0000-000000000000', '11111111-1111-1111-1111-111111111111') $$,
  'Ticket not found',
  'Cannot reassign non-existent ticket'
);

-- Test 3: Fails for ticket in progress
select throws_ok(
  $$ select public.admin_reassign_ticket('cccccccc-cccc-cccc-cccc-cccccccccccc', '11111111-1111-1111-1111-111111111111') $$,
  'Only waiting or called tickets can be reassigned',
  'Cannot reassign in_progress ticket'
);

-- Test 4: Fails for ticket belonging to appointment
select throws_ok(
  $$ select public.admin_reassign_ticket('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', '11111111-1111-1111-1111-111111111111') $$,
  'Cannot reassign a ticket that belongs to an appointment. Modify the appointment instead.',
  'Cannot reassign appointment ticket'
);

-- Test 5: Fails for inactive barber
select throws_ok(
  $$ select public.admin_reassign_ticket('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '22222222-2222-2222-2222-222222222222') $$,
  'Target barber must be active and available locally',
  'Cannot reassign to inactive barber'
);

-- Test 6: Fails for unavailable barber
select throws_ok(
  $$ select public.admin_reassign_ticket('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '33333333-3333-3333-3333-333333333333') $$,
  'Target barber must be active and available locally',
  'Cannot reassign to unavailable barber'
);

-- Test 7: Audit log was created
select is(
  (select count(*)::int from public.audit_log where action = 'admin_ticket_reassign_requested'),
  1,
  'Audit log was inserted'
);

-- Test 8: Command was created
select is(
  (select count(*)::int from public.ticket_admin_commands where status = 'pending'),
  1,
  'Command was created'
);

-- Test 9: Auth enforcement
create or replace function public.is_admin_or_owner() returns boolean language sql as $$ select false; $$;
select throws_ok(
  $$ select public.admin_reassign_ticket('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '11111111-1111-1111-1111-111111111111') $$,
  'Unauthorized: Must be an admin or owner to reassign tickets',
  'Fails if not admin'
);

select * from finish();
rollback;
