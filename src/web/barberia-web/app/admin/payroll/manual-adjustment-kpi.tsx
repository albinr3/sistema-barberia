"use client";

import { useActionState, useState } from "react";
import { Plus, X } from "lucide-react";
import { addPayrollAdjustment } from "@/app/actions/admin-payroll";
import type { PayrollBarber, PayrollWeekRange } from "@/lib/payroll";
import styles from "./payroll.module.css";

type ActionState = {
  success?: boolean;
  commandId?: string;
  error?: string;
} | null;

export function ManualAdjustmentKpi({
  value,
  sourceDeviceId,
  range,
  barbers,
  canRequestCommand,
}: {
  value: string;
  sourceDeviceId: string | null;
  range: PayrollWeekRange;
  barbers: PayrollBarber[];
  canRequestCommand: boolean;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const [state, action, pending] = useActionState<ActionState, FormData>(
    async (prevState, formData) => {
      const res = await addPayrollAdjustment(prevState, formData);
      if (res?.success) {
        setIsOpen(false);
      }
      return res;
    },
    null,
  );

  const commandDisabled = !sourceDeviceId || !canRequestCommand;

  return (
    <>
      <article className={`${styles.kpi} ${styles.kpiWithAction}`}>
        <div className={styles.kpiHeader}>
          <button
            type="button"
            className={styles.iconButton}
            onClick={() => setIsOpen(true)}
            title="Add adjustment"
            disabled={commandDisabled}
          >
            <Plus size={16} aria-hidden="true" />
          </button>
        </div>
        <span>Manual Adjustments</span>
        <strong>{value}</strong>
      </article>

      {isOpen && (
        <div className={styles.modalOverlay} role="dialog" aria-modal="true">
          <div className={styles.modal}>
            <header className={styles.modalHeader}>
              <div>
                <h2>Add Manual Adjustment</h2>
              </div>
              <button type="button" className={styles.iconButton} onClick={() => setIsOpen(false)} title="Close">
                <X size={18} aria-hidden="true" />
              </button>
            </header>
            <div style={{ padding: "18px" }}>
              <form action={action} className={styles.adjustmentFormModal}>
                <input type="hidden" name="sourceDeviceId" value={sourceDeviceId ?? ""} />
                <input type="hidden" name="startDate" value={range.startDate} />
                <input type="hidden" name="endDate" value={range.endDate} />
                
                <select name="barberId" required disabled={pending}>
                  <option value="">Select Barber</option>
                  {barbers.map((barber) => (
                    <option key={barber.id} value={barber.id}>
                      {barber.display_name ?? "Local barber"} {barber.station_code ? `(${barber.station_code})` : ""}
                    </option>
                  ))}
                </select>
                <input
                  name="amount"
                  type="number"
                  step="0.01"
                  placeholder="Amount (e.g. 50.00)"
                  required
                  disabled={pending}
                />
                <input
                  name="reason"
                  type="text"
                  placeholder="Reason"
                  required
                  disabled={pending}
                />
                <button type="submit" disabled={pending} className={styles.primaryButton}>
                  <Plus size={16} aria-hidden="true" />
                  {pending ? "Adding..." : "Add Adjustment"}
                </button>
                {state?.error && <span className={styles.errorText}>{state.error}</span>}
              </form>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
