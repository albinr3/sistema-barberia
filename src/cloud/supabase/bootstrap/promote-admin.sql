-- Run this manually after the user exists in Supabase Auth.
-- Replace the email and role before executing.

with promoted_user as (
  update public.profiles
  set
    role = 'owner',
    updated_at = now()
  where id = (
    select id
    from auth.users
    where lower(email) = lower('admin@example.com')
  )
  returning id
)
insert into public.audit_log (actor_id, action, entity_type, entity_id, metadata)
select
  id,
  'bootstrap_promote_admin',
  'profile',
  id::text,
  jsonb_build_object('role', 'owner', 'email', 'admin@example.com')
from promoted_user;

-- Optional verification.
select p.id, u.email, p.role, p.display_name
from public.profiles p
join auth.users u on u.id = p.id
where lower(u.email) = lower('admin@example.com');
