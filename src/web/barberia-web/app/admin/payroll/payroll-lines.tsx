"use client";

import { useState, useEffect } from "react";
import { Eye, X } from "lucide-react";
import { formatCurrency, formatDate, type PayrollLine, type PayrollPeriod } from "@/lib/payroll";
import { fetchLineDailyBreakdown, type DailyBreakdownRow } from "@/app/actions/admin-payroll";
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
          period={period}
          onClose={() => setSelectedLine(null)}
        />
      )}
    </>
  );
}

function LineDetailsModal({
  line,
  period,
  onClose,
}: {
  line: PayrollLine;
  period: PayrollPeriod;
  onClose: () => void;
}) {
  const [breakdown, setBreakdown] = useState<DailyBreakdownRow[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!line.barber_id) {
      setBreakdown([]);
      return;
    }
    
    fetchLineDailyBreakdown(
      line.barber_id,
      period.start_date,
      period.end_date,
      line.sales_generated_cents,
      line.commission_cents
    ).then((result) => {
      if (result.success) {
        setBreakdown(result.data);
      } else {
        setError(result.error);
      }
    });
  }, [line, period]);

  const formatDayDate = (isoString: string) => {
    const d = new Date(isoString + "T00:00:00Z");
    return new Intl.DateTimeFormat("en-US", { weekday: "short", month: "short", day: "numeric", year: "numeric", timeZone: "UTC" }).format(d);
  };

  return (
    <div className={styles.modalOverlay} role="dialog" aria-modal="true" aria-label={`${line.barber_name} payroll details`}>
      <div className={styles.modalWide}>
        <header className={styles.modalHeader}>
          <div>
            <h2>Earnings Breakdown - {line.barber_name}</h2>
            <p className={styles.modalSubtitle}>{formatDate(period.start_date)} - {formatDate(period.end_date)}</p>
          </div>
          <button type="button" className={styles.iconButton} onClick={onClose} title="Close">
            <X size={18} aria-hidden="true" />
          </button>
        </header>
        
        <div className={styles.modalBodyScroller}>
          {error ? (
            <div className={styles.emptyState}>Error loading breakdown: {error}</div>
          ) : !breakdown ? (
            <div className={styles.emptyState}>Loading daily breakdown...</div>
          ) : breakdown.length === 0 ? (
            <div className={styles.emptyState}>No activity recorded for this period.</div>
          ) : (
            <div className={styles.breakdownTableContainer}>
              <table className={styles.breakdownTable}>
                <thead>
                  <tr>
                    <th className={styles.textLeft}>DATE</th>
                    <th className={styles.textRight}>SERVICES</th>
                    <th className={styles.textRight}>DAILY SALES</th>
                    <th className={styles.textRight}>COMM. %</th>
                    <th className={styles.textRight}>COMMISSION($)</th>
                    <th className={styles.textRight}>EARNINGS</th>
                  </tr>
                </thead>
                <tbody>
                  {breakdown.map((row) => (
                    <tr key={row.date}>
                      <td className={styles.textLeft}>{formatDayDate(row.date)}</td>
                      <td className={styles.textRight}>
                        <span className={styles.servicesBadge}>{row.services}</span>
                      </td>
                      <td className={styles.textRight}>{formatCurrency(row.salesCents)}</td>
                      <td className={styles.textRight}>
                        <span className={styles.commBadge}>{(row.commissionPercentage * 100).toFixed(0)}%</span>
                      </td>
                      <td className={styles.textRight} style={{ fontWeight: 600 }}>{formatCurrency(row.commissionCents)}</td>
                      <td className={styles.textRight} style={{ fontWeight: 800, color: "var(--accent-strong)" }}>{formatCurrency(row.earningsCents)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        <footer className={styles.modalFooter}>
          <div className={styles.footerTotalLabel}>Total Period Earnings</div>
          <div className={styles.footerTotalValue}>{formatCurrency(line.total_cents)}</div>
        </footer>
      </div>
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
