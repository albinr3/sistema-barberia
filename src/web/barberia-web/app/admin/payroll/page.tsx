import Link from "next/link";
import type { Route } from "next";
import { CalendarDays, History, ShieldCheck, WifiOff } from "lucide-react";
import { AppShell } from "@/components/layout/app-shell";
import { requireAdmin } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";
import { formatCurrency, formatDate, getPayrollDashboard, payrollReference } from "@/lib/payroll";
import { PayrollActions } from "./payroll-actions";
import { PayrollLines } from "./payroll-lines";
import { ManualAdjustmentKpi } from "./manual-adjustment-kpi";
import styles from "./payroll.module.css";

export const dynamic = "force-dynamic";

export default async function AdminPayrollPage({
  searchParams,
}: {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const resolvedSearchParams = await searchParams;
  const selectedDate = typeof resolvedSearchParams.date === "string" ? resolvedSearchParams.date : undefined;
  const dashboard = await getPayrollDashboard(supabase, selectedDate);
  const { period, range, device } = dashboard;

  return (
    <AppShell title="Payroll Management" variant="admin">
      <div className={styles.page}>
        <section className={styles.titleRow}>
          <div>
            <div className={styles.badgeRow}>
              <span className={styles.periodBadge}>Current Period</span>
              <span className={period?.state === "paid" ? styles.paidBadge : styles.draftBadge}>
                {period?.state === "paid" ? "PAID" : "DRAFT"}
              </span>
              {dashboard.pendingCommand && <span className={styles.pendingBadge}>Pending Sync</span>}
              {dashboard.isSyncStale && (
                <span className={styles.warningBadge}>
                  <WifiOff size={14} aria-hidden="true" />
                  Stale
                </span>
              )}
            </div>
            <h2>Payroll Management</h2>
          </div>
          <Link href={"/admin/payroll/history" as Route} className={styles.historyLink}>
            <History size={16} aria-hidden="true" />
            View History
          </Link>
        </section>

        <section className={styles.kpiGrid}>
          <Kpi label="Total Services" value={(period?.total_services ?? 0).toString()} />
          <Kpi label="Total Commission" value={formatCurrency(period?.total_commission_cents ?? 0)} />
          <ManualAdjustmentKpi
            value={formatCurrency(period?.total_adjustments_cents ?? 0)}
            sourceDeviceId={device?.id ?? null}
            range={range}
            barbers={dashboard.barbers}
            canRequestCommand={dashboard.canRequestCommand}
          />
          <Kpi label="Net Pay (Total)" value={formatCurrency(period?.total_to_pay_cents ?? 0)} highlight />
        </section>

        <section className={styles.filtersBar}>
          <form className={styles.dateForm}>
            <CalendarDays size={18} aria-hidden="true" />
            <input type="date" name="date" defaultValue={range.referenceDate} />
            <button type="submit" className={styles.secondaryButton}>Load</button>
          </form>
          <div className={styles.rangeText}>{range.label}</div>
          <div className={styles.syncText}>
            <ShieldCheck size={16} aria-hidden="true" />
            {device?.last_sync_at ? `Last sync ${formatDate(device.last_sync_at)}` : "No desktop sync"}
            {dashboard.hasPendingOutbox ? `, ${device?.pending_outbox_count ?? 0} pending` : ""}
          </div>
        </section>

        {dashboard.lastCommand?.status === "failed" && (
          <div className={styles.errorBanner}>
            {dashboard.lastCommand.error_message ?? "Last payroll command failed."}
          </div>
        )}

        <PayrollActions
          sourceDeviceId={device?.id ?? null}
          range={range}
          barbers={dashboard.barbers}
          canRequestCommand={dashboard.canRequestCommand}
          canPay={dashboard.canPay}
          payBlockReason={dashboard.payBlockReason}
          reference={payrollReference(period, range)}
        />

        <PayrollLines period={period} />
      </div>
    </AppShell>
  );
}

function Kpi({ label, value, highlight = false }: { label: string; value: string; highlight?: boolean }) {
  return (
    <article className={styles.kpi}>
      <span>{label}</span>
      <strong className={highlight ? styles.highlightValue : undefined}>{value}</strong>
    </article>
  );
}
