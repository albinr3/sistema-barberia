/* eslint-disable @typescript-eslint/no-explicit-any */
"use client";

import { useState } from "react";
import { cancelCustomerAppointment } from "@/app/actions/booking";
import { AppointmentQrCode } from "@/components/booking/appointment-qr";
import Link from "next/link";
import styles from "./customer-appointments.module.css";

// Icons
const CalendarIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" className={styles.icon} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect width="18" height="18" x="3" y="4" rx="2" ry="2"/><line x1="16" x2="16" y1="2" y2="6"/><line x1="8" x2="8" y1="2" y2="6"/><line x1="3" x2="21" y1="10" y2="10"/></svg>
);

const ClockIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" className={styles.icon} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
);

const ScissorsIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" className={styles.icon} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="6" cy="6" r="3"/><circle cx="6" cy="18" r="3"/><line x1="20" x2="8.12" y1="4" y2="15.88"/><line x1="14.47" x2="14.48" y1="14.48" y2="14.48"/><line x1="20" x2="8.12" y1="20" y2="8.12"/></svg>
);

const PlusIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" className={styles.icon} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="12" x2="12" y1="5" y2="19"/><line x1="5" x2="19" y1="12" y2="12"/></svg>
);

const CheckIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" className={styles.icon} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>
);

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
        return <span className={`${styles.badge} ${styles.badgePending}`}>Pending</span>;
      case "confirmed":
        return <span className={`${styles.badge} ${styles.badgeConfirmed}`}>Confirmed</span>;
      case "cancelled":
        return <span className={`${styles.badge} ${styles.badgeCancelled}`}>Cancelled</span>;
      case "completed":
        return <span className={`${styles.badge} ${styles.badgeCompleted}`}>Completed</span>;
      case "no_show":
        return <span className={`${styles.badge} ${styles.badgeNoShow}`}>No Show</span>;
      default:
        return null;
    }
  };

  if (!appointments.length) {
    return (
      <div className={styles.emptyState}>
        <div className={styles.emptyIcon}>
          <CalendarIcon />
        </div>
        <p className={styles.emptyText}>You have no upcoming appointments.</p>
        <Link href="/app/book" className={styles.btnPrimary}>
          <PlusIcon /> Book an appointment
        </Link>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      {appointments.map(app => {
        const date = new Date(app.starts_at);
        const isFuture = date > new Date();
        const canCancel = isFuture && ["pending", "confirmed"].includes(app.status);
        const canUseQr = ["pending", "confirmed"].includes(app.status) && app.appointment_code;

        return (
          <div key={app.id} className={styles.card}>
            <div className={styles.cardTop}>
              <div className={styles.serviceInfo}>
                <h3 className={styles.serviceName}>{(app.service as any)?.name}</h3>
                <div className={styles.barberName}>
                  <ScissorsIcon /> {(app.barber as any)?.display_name}
                </div>
              </div>
              <div>
                {getStatusBadge(app.status)}
              </div>
            </div>

            <div className={styles.cardBody}>
              <div className={styles.dateTime}>
                <div className={styles.dateRow}>
                  <CalendarIcon /> 
                  <span className="capitalize">
                    {date.toLocaleDateString('en-US', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' })}
                  </span>
                </div>
                <div className={styles.timeRow}>
                  <ClockIcon /> {date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                </div>
                
                {app.cancellation_reason && (
                  <div className={styles.cancelReason}>
                    <svg xmlns="http://www.w3.org/2000/svg" className={styles.icon} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" x2="12" y1="8" y2="12"/><line x1="12" x2="12.01" y1="16" y2="16"/></svg>
                    <span>Cancellation reason: {app.cancellation_reason}</span>
                  </div>
                )}

                {app.status === "completed" && (
                  <div className={styles.completedMessage}>
                    <CheckIcon />
                    <span>Service completed! Thank you for your visit.</span>
                  </div>
                )}
              </div>

              {canUseQr && (
                <div className={styles.qrSection}>
                  <div className={styles.qrCode}>
                    <AppointmentQrCode value={app.appointment_code} size={90} />
                  </div>
                  <div className={styles.qrInfo}>
                    <span className={styles.qrCodeText}>{app.appointment_code}</span>
                    <span className={styles.qrText}>Show this QR code to the barber when you arrive.</span>
                  </div>
                </div>
              )}
            </div>

            {canCancel && (
              <div className={styles.actions}>
                <button 
                  onClick={() => handleCancel(app.id)}
                  disabled={cancelling === app.id}
                  className={styles.btnSecondary}
                >
                  {cancelling === app.id ? "Cancelling..." : "Cancel appointment"}
                </button>
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
