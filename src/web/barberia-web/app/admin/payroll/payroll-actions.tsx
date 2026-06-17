"use client";

import { useActionState } from "react";
import { RefreshCw } from "lucide-react";
import { requestPayrollSnapshot } from "@/app/actions/admin-payroll";
import type { PayrollWeekRange } from "@/lib/payroll";
import styles from "./payroll.module.css";

type ActionState = {
  success?: boolean;
  commandId?: string;
  error?: string;
} | null;

type PayrollActionsProps = {
  sourceDeviceId: string | null;
  range: PayrollWeekRange;
  canRequestCommand: boolean;
  reference: string;
};

export function PayrollActions({
  sourceDeviceId,
  range,
  canRequestCommand,
  reference,
}: PayrollActionsProps) {
  const [snapshotState, snapshotAction, snapshotPending] = useActionState<ActionState, FormData>(
    requestPayrollSnapshot,
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

      <div className={styles.payForm}>
        <label className={styles.referenceLabel}>
          <span>Payroll Reference</span>
          <input value={reference} readOnly tabIndex={-1} />
        </label>
        <span className={styles.blockReason}>Desktop marks closed payroll periods as paid automatically.</span>
      </div>
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
