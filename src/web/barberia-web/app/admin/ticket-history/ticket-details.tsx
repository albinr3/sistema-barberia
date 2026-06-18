"use client";

import { useEffect } from "react";
import styles from "./ticket-history.module.css";
import { formatCurrency, formatDateTime, formatStatus, getStatusCssKey, getSource, type TicketHistoryRow } from "@/lib/ticket-history";

export function TicketDetails({
  ticket,
  onClose,
}: {
  ticket: TicketHistoryRow | null;
  onClose: () => void;
}) {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    if (ticket) {
      document.addEventListener("keydown", handleKeyDown);
    }
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [ticket, onClose]);

  if (!ticket) return null;

  const handleBackdropClick = (e: React.MouseEvent) => {
    if (e.target === e.currentTarget) onClose();
  };

  const payment = ticket.payment;
  const items = ticket.items || [];
  const serviceName = items.length > 0 && items[0].service ? items[0].service.name : "-";
  const totalAmount = payment ? payment.amount_cents : items.reduce((sum, item) => sum + item.price_cents, 0);

  return (
    <div className={styles.modalOverlay} onClick={handleBackdropClick}>
      <div className={styles.modalContent}>
        <div className={styles.modalHeader}>
          <h2>Ticket #{ticket.display_ticket_number || ticket.local_ticket_id.split("-")[0]} Details</h2>
          <button className={styles.closeButton} onClick={onClose} aria-label="Close modal">
            &times;
          </button>
        </div>
        <div className={styles.modalBody}>
          <div className={styles.detailSection}>
            <h3>Summary</h3>
            <div className={styles.detailGrid}>
              <div className={styles.detailItem}>
                <span className={styles.detailLabel}>Internal ID</span>
                <span className={styles.detailValue}>{ticket.local_ticket_id}</span>
              </div>
              <div className={styles.detailItem}>
                <span className={styles.detailLabel}>Customer</span>
                <span className={styles.detailValue}>{ticket.customer_name || "Walk-in"}</span>
              </div>
              <div className={styles.detailItem}>
                <span className={styles.detailLabel}>Barber</span>
                <span className={styles.detailValue}>{ticket.barber?.display_name || "-"}</span>
              </div>
              <div className={styles.detailItem}>
                <span className={styles.detailLabel}>Service</span>
                <span className={styles.detailValue}>{serviceName}</span>
              </div>
              <div className={styles.detailItem}>
                <span className={styles.detailLabel}>Status</span>
                <span className={styles.detailValue}>
                  <span className={`${styles.statusBadge} ${styles[getStatusCssKey(ticket.status)] || ""}`}>
                    {formatStatus(ticket.status)}
                  </span>
                  {ticket.restore_reverted_at && (
                    <span className={`${styles.statusBadge} ${styles.reverted}`}>
                      Reverted by restore
                    </span>
                  )}
                </span>
              </div>
              <div className={styles.detailItem}>
                <span className={styles.detailLabel}>Source</span>
                <span className={styles.detailValue}>{getSource(ticket)}</span>
              </div>
            </div>
          </div>

          {ticket.restore_reverted_at && (
            <div className={styles.detailSection}>
              <h3>Restore Audit</h3>
              <div className={styles.detailGrid}>
                <div className={styles.detailItem}>
                  <span className={styles.detailLabel}>Restore ID</span>
                  <span className={styles.detailValue}>{ticket.restore_reverted_by || "-"}</span>
                </div>
                <div className={styles.detailItem}>
                  <span className={styles.detailLabel}>Reverted At</span>
                  <span className={styles.detailValue}>{formatDateTime(ticket.restore_reverted_at)}</span>
                </div>
                <div className={styles.detailItem}>
                  <span className={styles.detailLabel}>Reason</span>
                  <span className={styles.detailValue}>{ticket.restore_revert_reason || "Missing from restored desktop backup"}</span>
                </div>
              </div>
            </div>
          )}

          {(payment || totalAmount > 0) && (
            <div className={styles.detailSection}>
              <h3>Payment & Receipt</h3>
              <div className={styles.detailGrid}>
                <div className={styles.detailItem}>
                  <span className={styles.detailLabel}>Total</span>
                  <span className={styles.detailValue}>{formatCurrency(totalAmount)}</span>
                </div>
                <div className={styles.detailItem}>
                  <span className={styles.detailLabel}>Method</span>
                  <span className={styles.detailValue}>{payment ? payment.payment_method.toUpperCase() : "-"}</span>
                </div>
                <div className={styles.detailItem}>
                  <span className={styles.detailLabel}>Receipt Number</span>
                  <span className={styles.detailValue}>{payment?.receipt_number || "-"}</span>
                </div>
                <div className={styles.detailItem}>
                  <span className={styles.detailLabel}>Payment Ref.</span>
                  <span className={styles.detailValue}>{payment?.payment_reference || "-"}</span>
                </div>
                <div className={styles.detailItem}>
                  <span className={styles.detailLabel}>Collected At</span>
                  <span className={styles.detailValue}>{payment?.collected_at ? formatDateTime(payment.collected_at) : "-"}</span>
                </div>
              </div>
            </div>
          )}

          <div className={styles.detailSection}>
            <h3>Timeline</h3>
            <div className={styles.timeline}>
              <div className={styles.timelineEvent}>
                <div className={styles.timelineDot} style={{ backgroundColor: "#3b82f6" }}></div>
                <div className={styles.timelineContent}>
                  <div className={styles.timelineTitle}>Created / Checked In</div>
                  <div className={styles.timelineTime}>{formatDateTime(ticket.created_at)}</div>
                </div>
              </div>

              {ticket.started_at && (
                <div className={styles.timelineEvent}>
                  <div className={styles.timelineDot} style={{ backgroundColor: "#eab308" }}></div>
                  <div className={styles.timelineContent}>
                    <div className={styles.timelineTitle}>Service Started</div>
                    <div className={styles.timelineTime}>{formatDateTime(ticket.started_at)}</div>
                  </div>
                </div>
              )}

              {ticket.completed_at && (
                <div className={styles.timelineEvent}>
                  <div className={styles.timelineDot} style={{ backgroundColor: "#22c55e" }}></div>
                  <div className={styles.timelineContent}>
                    <div className={styles.timelineTitle}>Completed</div>
                    <div className={styles.timelineTime}>{formatDateTime(ticket.completed_at)}</div>
                  </div>
                </div>
              )}

              {ticket.cancelled_at && (
                <div className={styles.timelineEvent}>
                  <div className={styles.timelineDot} style={{ backgroundColor: "#ef4444" }}></div>
                  <div className={styles.timelineContent}>
                    <div className={styles.timelineTitle}>Cancelled</div>
                    <div className={styles.timelineTime}>{formatDateTime(ticket.cancelled_at)}</div>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
