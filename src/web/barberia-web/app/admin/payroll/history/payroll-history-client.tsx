"use client";

import { useState } from "react";
import Link from "next/link";
import type { Route } from "next";
import { ArrowLeft, Eye, X } from "lucide-react";
import { formatCurrency, formatDate, payrollReference, type PayrollPeriod } from "@/lib/payroll";
import styles from "../payroll.module.css";

export function PayrollHistoryClient({ periods }: { periods: PayrollPeriod[] }) {
  const [selectedPeriod, setSelectedPeriod] = useState<PayrollPeriod | null>(null);

  return (
    <div className={styles.page}>
      <div className={styles.backRow}>
        <Link href={"/admin/payroll" as Route} className={styles.backLink}>
          <ArrowLeft size={16} aria-hidden="true" />
          Back to Payroll
        </Link>
      </div>

      {periods.length === 0 ? (
        <div className={styles.emptyState}>No processed payroll periods found.</div>
      ) : (
        <div className={styles.tableScroller}>
          <table className={styles.historyTable}>
            <thead>
              <tr>
                <th>Reference #</th>
                <th>Period Date</th>
                <th>Processed Date</th>
                <th>Total Staff</th>
                <th>Total Payout</th>
                <th>Status</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {periods.map((period) => (
                <tr key={period.id}>
                  <td>{payrollReference(period, { referenceDate: period.start_date, startDate: period.start_date, endDate: period.end_date, label: "" })}</td>
                  <td>
                    {formatDate(period.start_date)} - {formatDate(addDaysToDate(period.end_date, -1))}
                  </td>
                  <td>{formatDate(period.paid_at ?? period.generated_at)}</td>
                  <td className={styles.numeric}>{period.lines.length}</td>
                  <td className={styles.netCell}>{formatCurrency(period.total_to_pay_cents)}</td>
                  <td>
                    <span className={styles.paidBadge}>Paid</span>
                  </td>
                  <td className={styles.actionCell}>
                    <button type="button" className={styles.iconButton} onClick={() => setSelectedPeriod(period)} title="View details">
                      <Eye size={16} aria-hidden="true" />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {selectedPeriod && <HistoryDetails period={selectedPeriod} onClose={() => setSelectedPeriod(null)} />}
    </div>
  );
}

function HistoryDetails({ period, onClose }: { period: PayrollPeriod; onClose: () => void }) {
  return (
    <div className={styles.modalOverlay} role="dialog" aria-modal="true" aria-label="Payroll history details">
      <div className={styles.modalWide}>
        <header className={styles.modalHeader}>
          <div>
            <h2>Payroll Details</h2>
            <p>{payrollReference(period, { referenceDate: period.start_date, startDate: period.start_date, endDate: period.end_date, label: "" })}</p>
          </div>
          <button type="button" className={styles.iconButton} onClick={onClose} title="Close">
            <X size={18} aria-hidden="true" />
          </button>
        </header>
        <div className={styles.tableScroller}>
          <table className={styles.payrollTable}>
            <thead>
              <tr>
                <th>Staff Member</th>
                <th>Services</th>
                <th>Sales</th>
                <th>Commission</th>
                <th>Adjustments</th>
                <th>Net Pay</th>
              </tr>
            </thead>
            <tbody>
              {period.lines.map((line) => (
                <tr key={line.id}>
                  <td>{line.barber_name}</td>
                  <td className={styles.numeric}>{line.closed_services_count}</td>
                  <td className={styles.numeric}>{formatCurrency(line.sales_generated_cents)}</td>
                  <td className={styles.numeric}>{formatCurrency(line.commission_cents)}</td>
                  <td className={styles.numeric}>{formatCurrency(line.adjustments_cents)}</td>
                  <td className={styles.netCell}>{formatCurrency(line.total_cents)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

function addDaysToDate(value: string, days: number) {
  const date = new Date(`${value}T00:00:00.000Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}
