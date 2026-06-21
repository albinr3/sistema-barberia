"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import type { Route } from "next";
import { ArrowLeft, Eye, Printer, X } from "lucide-react";
import { fetchLineDailyBreakdown, type DailyBreakdownRow } from "@/app/actions/admin-payroll";
import { formatCurrency, formatDate, payrollReference, type PayrollLine, type PayrollPeriod } from "@/lib/payroll";
import styles from "../payroll.module.css";

export function PayrollHistoryClient({ periods }: { periods: PayrollPeriod[] }) {
  const [selectedPeriod, setSelectedPeriod] = useState<PayrollPeriod | null>(null);
  const [printPeriod, setPrintPeriod] = useState<PayrollPeriod | null>(null);

  useEffect(() => {
    const clearPrintPeriod = () => setPrintPeriod(null);
    window.addEventListener("afterprint", clearPrintPeriod);
    return () => window.removeEventListener("afterprint", clearPrintPeriod);
  }, []);

  const printPayroll = (period: PayrollPeriod) => {
    setPrintPeriod(period);
    window.setTimeout(() => window.print(), 0);
  };

  return (
    <>
      <div className={`${styles.page} ${printPeriod ? styles.historyScreenPrinting : ""}`}>
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
                      <div className={styles.historyActions}>
                        <button type="button" className={styles.iconButton} onClick={() => setSelectedPeriod(period)} title="View details">
                          <Eye size={16} aria-hidden="true" />
                        </button>
                        <button type="button" className={styles.iconButton} onClick={() => printPayroll(period)} title="Print payroll">
                          <Printer size={16} aria-hidden="true" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {selectedPeriod && <HistoryDetails period={selectedPeriod} onClose={() => setSelectedPeriod(null)} />}
      </div>
      {printPeriod && <HistoryPrintReport period={printPeriod} />}
    </>
  );
}

function HistoryDetails({ period, onClose }: { period: PayrollPeriod; onClose: () => void }) {
  const [selectedLine, setSelectedLine] = useState<PayrollLine | null>(null);

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
                <th>Net Pay</th>
              </tr>
            </thead>
            <tbody>
              {period.lines.map((line) => (
                <tr key={line.id}>
                  <td>
                    <button type="button" className={styles.staffLinkButton} onClick={() => setSelectedLine(line)}>
                      {line.barber_name}
                    </button>
                  </td>
                  <td className={styles.numeric}>{line.closed_services_count}</td>
                  <td className={styles.numeric}>{formatCurrency(line.sales_generated_cents)}</td>
                  <td className={styles.numeric}>{formatCurrency(line.commission_cents)}</td>
                  <td className={styles.netCell}>{formatCurrency(line.total_cents)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {selectedLine && (
        <HistoryLineDetailsModal
          line={selectedLine}
          period={period}
          onClose={() => setSelectedLine(null)}
        />
      )}
    </div>
  );
}

function HistoryLineDetailsModal({
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
    setError(null);

    if (!line.barber_id) {
      setBreakdown([]);
      return;
    }

    setBreakdown(null);
    fetchLineDailyBreakdown(
      line.barber_id,
      period.start_date,
      period.end_date,
      line.sales_generated_cents,
      line.commission_cents,
    ).then((result) => {
      if (result.success) {
        setBreakdown(result.data);
      } else {
        setError(result.error);
      }
    });
  }, [line, period]);

  return (
    <div className={styles.modalOverlay} role="dialog" aria-modal="true" aria-label={`${line.barber_name} payroll details`}>
      <div className={styles.modalWide}>
        <header className={styles.modalHeader}>
          <div>
            <h2>Earnings Breakdown - {line.barber_name}</h2>
            <p>{formatDate(period.start_date)} - {formatDate(addDaysToDate(period.end_date, -1))}</p>
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
                      <td className={styles.textRight}>{formatCurrency(row.commissionCents)}</td>
                      <td className={styles.textRight}>{formatCurrency(row.earningsCents)}</td>
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

function HistoryPrintReport({ period }: { period: PayrollPeriod }) {
  const reference = payrollReference(period, { referenceDate: period.start_date, startDate: period.start_date, endDate: period.end_date, label: "" });

  return (
    <section className={styles.historyPrintArea} aria-hidden="true">
      <header className={styles.printReportHeader}>
        <div>
          <h1>Payroll Details</h1>
          <p>{reference}</p>
        </div>
        <div className={styles.printReportMeta}>
          <span>{formatDate(period.start_date)} - {formatDate(addDaysToDate(period.end_date, -1))}</span>
          <span>Processed {formatDate(period.paid_at ?? period.generated_at)}</span>
        </div>
      </header>

      <section className={styles.printSummaryGrid}>
        <div>
          <span>Total Staff</span>
          <strong>{period.lines.length}</strong>
        </div>
        <div>
          <span>Total Services</span>
          <strong>{period.total_services}</strong>
        </div>
        <div>
          <span>Total Payout</span>
          <strong>{formatCurrency(period.total_to_pay_cents)}</strong>
        </div>
      </section>

      <table className={styles.payrollTable}>
        <thead>
          <tr>
            <th>Staff Member</th>
            <th>Services</th>
            <th>Sales</th>
            <th>Commission</th>
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
              <td className={styles.netCell}>{formatCurrency(line.total_cents)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}

function addDaysToDate(value: string, days: number) {
  const date = new Date(`${value}T00:00:00.000Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

function formatDayDate(value: string) {
  const date = new Date(`${value}T00:00:00.000Z`);
  return new Intl.DateTimeFormat("en-US", {
    weekday: "short",
    month: "short",
    day: "numeric",
    year: "numeric",
    timeZone: "UTC",
  }).format(date);
}

