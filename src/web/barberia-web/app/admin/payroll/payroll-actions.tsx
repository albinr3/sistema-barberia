"use client";

import { useActionState } from "react";
import { CircleDollarSign, RefreshCw } from "lucide-react";
import { requestPayrollPayment, requestPayrollSnapshot } from "@/app/actions/admin-payroll";
import type { PayrollBarber, PayrollWeekRange } from "@/lib/payroll";
import styles from "./payroll.module.css";

type ActionState = {
  success?: boolean;
  commandId?: string;
  error?: string;
} | null;

type PayrollActionsProps = {
  sourceDeviceId: string | null;
  range: PayrollWeekRange;
  barbers: PayrollBarber[];
  canRequestCommand: boolean;
  canPay: boolean;
  payBlockReason: string | null;
  reference: string;
};

export function PayrollActions({
  sourceDeviceId,
  range,
  barbers,
  canRequestCommand,
  canPay,
  payBlockReason,
  reference,
}: PayrollActionsProps) {
  const [snapshotState, snapshotAction, snapshotPending] = useActionState<ActionState, FormData>(
    requestPayrollSnapshot,
    null,
  );
  const [paymentState, paymentAction, paymentPending] = useActionState<ActionState, FormData>(
    async (previousState, formData) => {
      if (!confirm("Request payroll payment from desktop?")) {
        return previousState;
      }

      return requestPayrollPayment(previousState, formData);
    },
    null,
  );

  const commandDisabled = !sourceDeviceId || !canRequestCommand;

  return (
    <div className={styles.actionsGrid}>
      <form action={snapshotAction} className={styles.inlineAction}>
        <HiddenCommandFields sourceDeviceId={sourceDeviceId} range={range} />
        <button type="submit" disabled={commandDisabled || snapshotPending} className={styles.secondaryButton}>
          <RefreshCw size={16} aria-hidden="true" />
          {snapshotPending ? "Requesting" : "Recalculate"}
        </button>
        <ActionMessage state={snapshotState} successText="Recalculate request sent." />
      </form>

      <form action={paymentAction} className={styles.payForm}>
        <HiddenCommandFields sourceDeviceId={sourceDeviceId} range={range} />
        
        <label className={styles.referenceLabel}>
          <span>Payroll Reference</span>
          <input value={reference} readOnly tabIndex={-1} />
        </label>

        <input type="hidden" name="paymentMethod" value="cash" />
        <button type="submit" disabled={!canPay || paymentPending} className={styles.primaryButton} title={payBlockReason ?? "Pay Payroll"}>
          <CircleDollarSign size={18} aria-hidden="true" />
          {paymentPending ? "Requesting" : "Pay Payroll"}
        </button>
        {payBlockReason && <span className={styles.blockReason}>{payBlockReason}</span>}
        <ActionMessage state={paymentState} successText="Payment request sent." />
      </form>
    </div>
  );
}

function HiddenCommandFields({
  sourceDeviceId,
  range,
}: {
  sourceDeviceId: string | null;
  range: PayrollWeekRange;
}) {
  return (
    <>
      <input type="hidden" name="sourceDeviceId" value={sourceDeviceId ?? ""} />
      <input type="hidden" name="startDate" value={range.startDate} />
      <input type="hidden" name="endDate" value={range.endDate} />
    </>
  );
}

function ActionMessage({ state, successText }: { state: ActionState; successText: string }) {
  if (!state) return null;
  if (state.error) return <span className={styles.errorText}>{state.error}</span>;
  if (state.success) return <span className={styles.successText}>{successText}</span>;
  return null;
}
