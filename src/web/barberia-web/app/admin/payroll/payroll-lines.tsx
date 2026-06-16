"use client";

import { useState } from "react";
import { Eye, X } from "lucide-react";
import { formatCurrency, type PayrollAdjustment, type PayrollLine, type PayrollPeriod } from "@/lib/payroll";
import styles from "./payroll.module.css";

export function PayrollLines({ period }: { period: PayrollPeriod | null }) {
  const [selectedLine, setSelectedLine] = useState<PayrollLine | null>(null);

  if (!period || period.lines.length === 0) {
    return <div className={styles.emptyState}>No commissions for this week.</div>;
  }

  return (
    <>
      <div className={styles.tableScroller}>
        <table className={styles.payrollTable}>
          <thead>
            <tr>
              <th>Staff Member</th>
              <th>Station</th>
              <th>Services</th>
              <th>Sales</th>
              <th>Comm. $</th>
              <th>Net Pay</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {period.lines.map((line) => (
              <tr key={line.id}>
                <td>
                  <div className={styles.staffCell}>
                    <span className={styles.avatar}>{initials(line.barber_name)}</span>
                    <strong>{line.barber_name}</strong>
                  </div>
                </td>
                <td>
                  <span className={styles.stationBadge}>
                    {line.station_number ? `B-${line.station_number}` : "N/A"}
                  </span>
                </td>
                <td className={styles.numeric}>{line.closed_services_count}</td>
                <td className={styles.numeric}>{formatCurrency(line.sales_generated_cents)}</td>
                <td className={styles.numeric}>{formatCurrency(line.commission_cents)}</td>
                <td className={styles.netCell}>{formatCurrency(line.total_cents)}</td>
                <td className={styles.actionCell}>
                  <button type="button" className={styles.iconButton} onClick={() => setSelectedLine(line)} title="View details">
                    <Eye size={16} aria-hidden="true" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {selectedLine && (
        <LineDetailsModal
          line={selectedLine}
          adjustments={period.adjustments.filter((adjustment) => adjustment.barber_id === selectedLine.barber_id)}
          onClose={() => setSelectedLine(null)}
        />
      )}
    </>
  );
}

function LineDetailsModal({
  line,
  adjustments,
  onClose,
}: {
  line: PayrollLine;
  adjustments: PayrollAdjustment[];
  onClose: () => void;
}) {
  return (
    <div className={styles.modalOverlay} role="dialog" aria-modal="true" aria-label={`${line.barber_name} payroll details`}>
      <div className={styles.modal}>
        <header className={styles.modalHeader}>
          <div>
            <h2>{line.barber_name}</h2>
            <p>{line.station_number ? `Station B-${line.station_number}` : "No station"}</p>
          </div>
          <button type="button" className={styles.iconButton} onClick={onClose} title="Close">
            <X size={18} aria-hidden="true" />
          </button>
        </header>
        <div className={styles.detailGrid}>
          <Detail label="Services" value={line.closed_services_count.toString()} />
          <Detail label="Sales" value={formatCurrency(line.sales_generated_cents)} />
          <Detail label="Commission" value={formatCurrency(line.commission_cents)} />
          <Detail label="Adjustments" value={formatCurrency(line.adjustments_cents)} />
          <Detail label="Net Pay" value={formatCurrency(line.total_cents)} strong />
        </div>
        <section className={styles.adjustmentsList}>
          <h3>Manual Adjustments</h3>
          {adjustments.length === 0 ? (
            <p>No adjustments for this barber.</p>
          ) : (
            <ul>
              {adjustments.map((adjustment) => (
                <li key={adjustment.id}>
                  <span>{adjustment.reason}</span>
                  <strong>{formatCurrency(adjustment.amount_cents)}</strong>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  );
}

function Detail({ label, value, strong = false }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className={styles.detailItem}>
      <span>{label}</span>
      <strong className={strong ? styles.detailStrong : undefined}>{value}</strong>
    </div>
  );
}

function initials(name: string) {
  const value = name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");
  return value || "?";
}
