import { AppShell } from "@/components/layout/app-shell";
import { requireCustomer } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";
import { getCustomerAppointments } from "@/lib/booking/queries";
import { CustomerAppointmentsList } from "@/components/booking/customer-appointments";

export default async function AppointmentsPage() {
  const supabase = await createClient();
  await requireCustomer(supabase);

  const appointments = await getCustomerAppointments(supabase);

  return (
    <AppShell title="My appointments" variant="customer">
      <div className="max-w-4xl mx-auto">
        <CustomerAppointmentsList appointments={appointments} />
      </div>
    </AppShell>
  );
}
