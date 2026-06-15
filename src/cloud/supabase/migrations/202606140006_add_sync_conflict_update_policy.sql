create policy "Admins can update sync conflicts"
  on public.sync_conflicts for update
  to authenticated
  using (public.is_admin_or_owner())
  with check (public.is_admin_or_owner());
