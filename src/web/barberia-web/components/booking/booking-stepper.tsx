/* eslint-disable @typescript-eslint/no-explicit-any, @typescript-eslint/no-unused-vars */
"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { getAvailableSlotsAction, createAppointment } from "@/app/actions/booking";
import styles from "./BookingStepper.module.css";

type Step = "SERVICE" | "BARBER" | "DATETIME" | "CONFIRM";

export function BookingStepper({ services, barbers }: any) {
  const router = useRouter();
  const [step, setStep] = useState<Step>("SERVICE");
  const [serviceId, setServiceId] = useState<string | null>(null);
  const [barberId, setBarberId] = useState<string | null>(null);
  const [date, setDate] = useState<string>("");
  const [slots, setSlots] = useState<any[]>([]);
  const [selectedSlot, setSelectedSlot] = useState<any | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const selectedService = services.find((s: any) => s.id === serviceId);
  const selectedBarber = barbers.find((b: any) => b.id === barberId);

  const availableBarbers = barbers;
  const fetchSlots = async (selectedDate: string) => {
    setDate(selectedDate);
    setSelectedSlot(null);
    if (!serviceId || !selectedDate) return;
    
    setLoading(true);
    setError(null);
    const res = await getAvailableSlotsAction(serviceId, selectedDate, barberId || undefined);
    setLoading(false);

    if (res.error) {
      setError(res.error);
    } else {
      setSlots(res.slots || []);
    }
  };

  const handleBook = async () => {
    if (!serviceId || !selectedSlot) return;
    
    setLoading(true);
    setError(null);
    const res = await createAppointment(serviceId, selectedSlot.barber_id, selectedSlot.starts_at);
    setLoading(false);

    if (res.error) {
      setError(res.error);
    } else {
      router.push("/app/appointments");
    }
  };

  return (
    <div className={styles.container}>
      {/* Progress */}
      <div className={styles.progress}>
        {["SERVICE", "BARBER", "DATETIME", "CONFIRM"].map((s, i) => (
          <div 
            key={s} 
            className={`${styles.progressStep} ${["SERVICE", "BARBER", "DATETIME", "CONFIRM"].indexOf(step) >= i ? styles.progressStepActive : ""}`} 
          />
        ))}
      </div>

      {error && (
        <div className={styles.error}>
          {error}
        </div>
      )}

      {step === "SERVICE" && (
        <div>
          <h2 className={styles.stepTitle}>Select a Service</h2>
          <div className={styles.servicesGrid}>
            {services.map((s: any) => (
              <div 
                key={s.id} 
                onClick={() => { setServiceId(s.id); setBarberId(null); setDate(""); setSlots([]); setSelectedSlot(null); setStep("BARBER"); }}
                className={styles.serviceCard}
              >
                <div className={styles.serviceInfo}>
                  <h3 className={styles.serviceName}>{s.name}</h3>
                  <span className={styles.serviceDuration}>{s.duration_minutes} mins</span>
                  {s.description && <p className={styles.serviceDesc}>{s.description}</p>}
                </div>
                <div className={styles.servicePrice}>
                  ${(s.base_price_cents / 100).toFixed(2)}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {step === "BARBER" && (
        <div>
          <button onClick={() => { setStep("SERVICE"); setBarberId(null); setDate(""); setSlots([]); setSelectedSlot(null); }} className={styles.backButton}>&larr; Back to services</button>
          <h2 className={styles.stepTitle}>Select a Barber</h2>
          <div className={styles.barbersGrid}>
              <>
                <div 
                  onClick={() => { setBarberId(null); setDate(""); setSlots([]); setSelectedSlot(null); setStep("DATETIME"); }}
                  className={`${styles.barberCard} ${styles.anyBarberCard}`}
                >
                  <h3 className={styles.barberName}>Any Available</h3>
                  <span className={styles.stationCode}>Next available slot</span>
                </div>
                {availableBarbers.map((b: any) => (
                  <div 
                    key={b.id} 
                    onClick={() => { setBarberId(b.id); setDate(""); setSlots([]); setSelectedSlot(null); setStep("DATETIME"); }}
                    className={styles.barberCard}
                  >
                    <h3 className={styles.barberName}>{b.display_name}</h3>
                    <span className={styles.stationCode}>{b.station_code ? `Station ${b.station_code}` : "N/A"}</span>
                  </div>
                ))}
              </>
          </div>
        </div>
      )}

      {step === "DATETIME" && (
        <div>
          <button onClick={() => { setStep("BARBER"); setDate(""); setSlots([]); setSelectedSlot(null); }} className={styles.backButton}>&larr; Back to barbers</button>
          <h2 className={styles.stepTitle}>Select Date & Time</h2>
          
          <div className={styles.dateInputGroup}>
            <label className={styles.dateLabel}>Date</label>
            <input 
              type="date" 
              className={styles.dateInput} 
              min={new Date().toLocaleDateString('en-CA', { timeZone: 'America/New_York' })}
              value={date}
              onChange={(e) => fetchSlots(e.target.value)}
            />
          </div>

          {loading ? (
            <div className={styles.loadingState}>
              Loading available slots...
            </div>
          ) : date ? (
            slots.length > 0 ? (
              <div className={styles.slotsGrid}>
                {slots.map((slot: any, i: number) => {
                  const timeStr = new Date(slot.starts_at).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
                  const isSelected = selectedSlot?.starts_at === slot.starts_at && selectedSlot?.barber_id === slot.barber_id;
                  
                  return (
                    <button 
                      key={i}
                      onClick={() => { setSelectedSlot(slot); setStep("CONFIRM"); }}
                      className={`${styles.slotButton} ${isSelected ? styles.slotButtonActive : ""}`}
                    >
                      <span>{timeStr}</span>
                      {!barberId && <span className={styles.slotBarberName}>{slot.barber_name}</span>}
                    </button>
                  );
                })}
              </div>
            ) : (
              <p className={styles.emptyState}>No available slots for this date.</p>
            )
          ) : (
            <p className={styles.emptyState}>Please select a date to view available times.</p>
          )}
        </div>
      )}

      {step === "CONFIRM" && selectedSlot && (
        <div>
          <button onClick={() => setStep("DATETIME")} className={styles.backButton}>&larr; Back to time selection</button>
          <h2 className={styles.stepTitle}>Confirm Appointment</h2>
          
          <div className={styles.receipt}>
            <div className={styles.receiptRow}>
              <div className={styles.receiptInfo}>
                <span className={styles.receiptLabel}>Service</span>
                <span className={styles.receiptValue}>{selectedService?.name}</span>
              </div>
              <div className={styles.receiptPrice}>
                ${(selectedService?.base_price_cents / 100).toFixed(2)}
              </div>
            </div>
            <div className={styles.receiptRow}>
              <div className={styles.receiptInfo}>
                <span className={styles.receiptLabel}>Barber</span>
                <span className={styles.receiptValue}>{selectedSlot.barber_name}</span>
              </div>
            </div>
            <div className={styles.receiptRow}>
              <div className={styles.receiptInfo}>
                <span className={styles.receiptLabel}>Date & Time</span>
                <span className={styles.receiptValue}>
                  {new Date(selectedSlot.starts_at).toLocaleDateString()} at {new Date(selectedSlot.starts_at).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                </span>
              </div>
            </div>
          </div>

          <button 
            className={styles.confirmButton}
            onClick={handleBook}
            disabled={loading}
          >
            {loading ? "Confirming..." : "Confirm & Book"}
          </button>
        </div>
      )}
    </div>
  );
}
