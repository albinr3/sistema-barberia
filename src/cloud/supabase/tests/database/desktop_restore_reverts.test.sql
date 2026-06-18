begin;

select plan(8);

select has_table('public', 'desktop_restore_batches', 'desktop restore audit table exists');

select has_column('public', 'synced_tickets', 'restore_reverted_at', 'tickets record restore revert timestamp');
select has_column('public', 'synced_tickets', 'restore_reverted_by', 'tickets record restore batch id');
select has_column('public', 'synced_tickets', 'restore_revert_reason', 'tickets record restore reason');

select has_column('public', 'synced_ticket_items', 'restore_reverted_at', 'ticket items record restore revert timestamp');
select has_column('public', 'synced_payments', 'restore_reverted_at', 'payments record restore revert timestamp');
select has_column('public', 'synced_payments', 'restore_reverted_by', 'payments record restore batch id');
select has_column('public', 'synced_payments', 'restore_revert_reason', 'payments record restore reason');

select * from finish();
rollback;
