import { AppShell } from "@/components/layout/app-shell";
import { requireAdmin } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";
import { getAdminAppointments, getBookingBarbers } from "@/lib/booking/queries";
import { AdminAppointmentsTable } from "@/components/booking/admin-appointments-table";

export default async function AdminAppointmentsPage() {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const [appointments, barbers] = await Promise.all([
    getAdminAppointments(supabase),
    getBookingBarbers(supabase)
  ]);

  return (
    <AppShell title="Manage Appointments" variant="admin">
      <div className="mb-6">
        <h1 className="text-3xl font-bold text-[#1a1c1e]">Appointments</h1>
        <p className="text-[#444655]">View and manage all customer appointments.</p>
      </div>

      <AdminAppointmentsTable appointments={appointments} barbers={barbers} />
    </AppShell>
  );
}
