/* eslint-disable @typescript-eslint/no-explicit-any */
import { AppShell } from "@/components/layout/app-shell";
import { requireBarber } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";
import { getBarberAppointments } from "@/lib/booking/queries";

export default async function BarberPage() {
  const supabase = await createClient();
  await requireBarber(supabase);

  const appointments = await getBarberAppointments(supabase);

  return (
    <AppShell title="Barber's schedule" variant="barber">
      <div className="max-w-4xl mx-auto">
        <h1 className="text-3xl font-bold text-[#1a1c1e] mb-6">Today&apos;s Schedule</h1>

        {appointments.length === 0 ? (
          <div className="bg-white p-8 rounded-lg border border-[#e8e8ea] text-center shadow-sm">
            <p className="text-[#444655]">No upcoming appointments found for your profile.</p>
          </div>
        ) : (
          <div className="space-y-4">
            {appointments.map(app => {
              const date = new Date(app.starts_at);
              return (
                <div key={app.id} className="bg-white p-6 rounded-lg border border-[#e8e8ea] shadow-sm flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
                  <div>
                    <div className="flex items-center gap-3 mb-2">
                      <h3 className="text-lg font-bold text-[#1a1c1e]">{(app.service as any)?.name}</h3>
                      <span className={`px-2 py-1 text-xs font-bold uppercase rounded-full ${
                        app.status === 'confirmed' ? 'bg-[#dfe0ff] text-[#000b62]' :
                        app.status === 'cancelled' ? 'bg-[#ffdad6] text-[#93000a]' :
                        app.status === 'completed' ? 'bg-[#e1e3e4] text-[#191c1d]' :
                        app.status === 'no_show' ? 'bg-[#ffb3ae] text-[#410005]' :
                        'bg-[#eeeef0] text-[#444655]'
                      }`}>
                        {app.status}
                      </span>
                    </div>
                    <p className="text-[#444655] mb-1">
                      <span className="font-semibold text-[#1a1c1e]">Customer:</span> {(app.customer as any)?.display_name || "Unknown"}
                    </p>
                    <p className="text-[#444655] mb-1">
                      <span className="font-semibold text-[#1a1c1e]">Phone:</span> {(app.customer as any)?.phone || "N/A"}
                    </p>
                    <p className="text-[#444655]">
                      <span className="font-semibold text-[#1a1c1e]">Time:</span> {date.toLocaleDateString()} at {date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                    </p>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </AppShell>
  );
}
