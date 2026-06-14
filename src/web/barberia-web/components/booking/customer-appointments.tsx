/* eslint-disable @typescript-eslint/no-explicit-any */
"use client";

import { useState } from "react";
import { cancelCustomerAppointment } from "@/app/actions/booking";
import { Button } from "@/components/ui/button";
import { AppointmentQrCode } from "@/components/booking/appointment-qr";

export function CustomerAppointmentsList({ appointments }: { appointments: any[] }) {
  const [cancelling, setCancelling] = useState<string | null>(null);

  const handleCancel = async (id: string) => {
    if (!confirm("Are you sure you want to cancel this appointment?")) return;
    setCancelling(id);
    await cancelCustomerAppointment(id);
    setCancelling(null);
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "pending":
      case "confirmed":
        return <span className="bg-[#dfe0ff] text-[#000b62] px-2 py-1 rounded-full text-xs font-bold uppercase">{status}</span>;
      case "cancelled":
        return <span className="bg-[#ffdad6] text-[#93000a] px-2 py-1 rounded-full text-xs font-bold uppercase">{status}</span>;
      case "completed":
        return <span className="bg-[#e1e3e4] text-[#191c1d] px-2 py-1 rounded-full text-xs font-bold uppercase">{status}</span>;
      case "no_show":
        return <span className="bg-[#ffb3ae] text-[#410005] px-2 py-1 rounded-full text-xs font-bold uppercase">No Show</span>;
      default:
        return null;
    }
  };

  if (!appointments.length) {
    return (
      <div className="bg-white p-8 rounded-lg border border-[#e8e8ea] text-center shadow-sm">
        <p className="text-[#444655] mb-4">You have no appointments yet.</p>
        <Button onClick={() => window.location.href = "/app/book"} className="bg-[#0020c2] text-white">Book an Appointment</Button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {appointments.map(app => {
        const date = new Date(app.starts_at);
        const isFuture = date > new Date();
        const canCancel = isFuture && ["pending", "confirmed"].includes(app.status);
        const canUseQr = canCancel && app.appointment_code;

        return (
          <div key={app.id} className="bg-white p-6 rounded-lg border border-[#e8e8ea] shadow-sm flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
            <div className="flex flex-col sm:flex-row gap-5">
              {canUseQr && (
                <div className="flex flex-col items-center gap-2">
                  <AppointmentQrCode value={app.appointment_code} size={112} />
                  <span className="font-mono text-xs font-semibold text-[#444655]">{app.appointment_code}</span>
                </div>
              )}
              <div>
              <div className="flex items-center gap-3 mb-2">
                <h3 className="text-lg font-bold text-[#1a1c1e]">{(app.service as any)?.name}</h3>
                {getStatusBadge(app.status)}
              </div>
              <p className="text-[#444655] mb-1">
                <span className="font-semibold text-[#1a1c1e]">Barber:</span> {(app.barber as any)?.display_name}
              </p>
              <p className="text-[#444655]">
                <span className="font-semibold text-[#1a1c1e]">Date:</span> {date.toLocaleDateString()} at {date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
              </p>
              {app.cancellation_reason && (
                <p className="text-sm text-[#93000a] mt-2 italic">Cancel reason: {app.cancellation_reason}</p>
              )}
              {canUseQr && (
                <p className="text-sm text-[#444655] mt-3">
                  Show this QR to your barber when you arrive.
                </p>
              )}
              </div>
            </div>

            {canCancel && (
              <Button 
                onClick={() => handleCancel(app.id)}
                disabled={cancelling === app.id}
                className="bg-transparent text-[#1a1c1e] hover:bg-[#f3f3f6] border border-[#e2e2e5]"
              >
                {cancelling === app.id ? "Cancelling..." : "Cancel"}
              </Button>
            )}
          </div>
        );
      })}
    </div>
  );
}
