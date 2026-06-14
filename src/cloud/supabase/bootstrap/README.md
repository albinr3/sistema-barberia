# Admin Bootstrap

Supabase Auth owns passwords and sessions. Do not store an admin password in this repository.

## Create The First Admin

1. Create the user in Supabase Auth using the dashboard, invite flow or CLI.
2. Run `promote-admin.sql` in the Supabase SQL editor with the email of that user.
3. Sign in from the web app. Only users with `profiles.role` of `admin` or `owner` can enter `/admin`.

Use `owner` for the first business administrator. Use `admin` for later staff accounts that need administrative access.
