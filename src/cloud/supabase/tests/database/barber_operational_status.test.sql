begin;

select plan(10);

select has_table('public', 'barber_operational_status', 'barber operational status projection exists');

select has_column('public', 'barber_operational_status', 'barber_id', 'operational status stores barber id');
select has_column('public', 'barber_operational_status', 'source_device_id', 'operational status stores source device id');
select has_column('public', 'barber_operational_status', 'business_date', 'operational status stores business date');
select has_column('public', 'barber_operational_status', 'state', 'operational status stores local barber state');
select has_column('public', 'barber_operational_status', 'clients_served_today', 'operational status stores clients served today');
select has_column('public', 'barber_operational_status', 'checked_in_at', 'operational status stores checked in timestamp');
select has_column('public', 'barber_operational_status', 'daily_queue_position', 'operational status stores daily queue position');
select has_column('public', 'barber_operational_status', 'daily_arrived_at', 'operational status stores daily arrival timestamp');
select has_column('public', 'barber_operational_status', 'is_checked_in_today', 'operational status stores daily check-in flag');

select * from finish();
rollback;
