"use client";

import { useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import styles from "./ticket-history.module.css";
import { TicketDetails } from "./ticket-details";
import type { Route } from "next";
import {
  type TicketHistoryResult,
  type TicketHistoryRow,
  formatCurrency,
  formatDateTime,
  formatStatus,
} from "@/lib/ticket-history";

export function TicketHistoryClient({ initialData }: { initialData: TicketHistoryResult }) {
  const router = useRouter();
  const searchParams = useSearchParams();

  const [search, setSearch] = useState(searchParams.get("search") || "");
  const [startDate, setStartDate] = useState(searchParams.get("startDate") || "");
  const [endDate, setEndDate] = useState(searchParams.get("endDate") || "");
  const [barberId, setBarberId] = useState(searchParams.get("barberId") || "");
  const [status, setStatus] = useState(searchParams.get("status") || "");

  const [selectedTicket, setSelectedTicket] = useState<TicketHistoryRow | null>(null);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    updateUrl(1);
  };

  const handleReset = () => {
    setSearch("");
    setStartDate("");
    setEndDate("");
    setBarberId("");
    setStatus("");
    router.push("/admin/ticket-history" as Route);
  };

  const handlePageChange = (newPage: number) => {
    updateUrl(newPage);
  };

  const updateUrl = (page: number) => {
    const params = new URLSearchParams();
    if (search) params.set("search", search);
    if (startDate) params.set("startDate", startDate);
    if (endDate) params.set("endDate", endDate);
    if (barberId) params.set("barberId", barberId);
    if (status) params.set("status", status);
    if (page > 1) params.set("page", page.toString());

    const query = params.toString();
    router.push(`/admin/ticket-history${query ? `?${query}` : ""}` as Route);
  };

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <p>Review completed, cancelled and in-progress tickets. This is a read-only view.</p>
      </header>

      <form className={styles.filters} onSubmit={handleSearch}>
        <div className={styles.filterGroup}>
          <label htmlFor="search">Search</label>
          <input
            id="search"
            type="text"
            placeholder="Ticket # or Customer..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>

        <div className={styles.filterGroup}>
          <label htmlFor="startDate">From</label>
          <input
            id="startDate"
            type="date"
            value={startDate}
            onChange={(e) => setStartDate(e.target.value)}
          />
        </div>

        <div className={styles.filterGroup}>
          <label htmlFor="endDate">To</label>
          <input
            id="endDate"
            type="date"
            value={endDate}
            onChange={(e) => setEndDate(e.target.value)}
          />
        </div>

        <div className={styles.filterGroup}>
          <label htmlFor="barberId">Barber</label>
          <select id="barberId" value={barberId} onChange={(e) => setBarberId(e.target.value)}>
            <option value="">All Barbers</option>
            {initialData.barbers.map((b) => (
              <option key={b.id} value={b.id}>
                {b.display_name}
              </option>
            ))}
          </select>
        </div>

        <div className={styles.filterGroup}>
          <label htmlFor="status">Status</label>
          <select id="status" value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">All Statuses</option>
            <option value="waiting">Waiting</option>
            <option value="called">Called</option>
            <option value="in_progress">In Progress</option>
            <option value="completed">Completed</option>
            <option value="cancelled">Cancelled</option>
          </select>
        </div>

        <button type="submit" className={styles.searchButton}>
          Apply
        </button>
        <button type="button" className={styles.resetButton} onClick={handleReset}>
          Reset
        </button>
      </form>

      {initialData.tickets.length === 0 ? (
        <div className={styles.empty}>No tickets found for the given filters.</div>
      ) : (
        <div className={styles.tableContainer}>
          <table className={styles.historyTable}>
            <thead>
              <tr>
                <th>TICKET ID</th>
                <th>DATE & TIME</th>
                <th>CUSTOMER</th>
                <th>BARBER</th>
                <th>SERVICE</th>
                <th>TOTAL</th>
                <th>METHOD</th>
                <th>STATUS</th>
                <th>ACTION</th>
              </tr>
            </thead>
            <tbody>
              {initialData.tickets.map((ticket) => {
                const serviceName =
                  ticket.items && ticket.items.length > 0 && ticket.items[0].service
                    ? ticket.items[0].service.name
                    : "Walk-in";
                const totalAmount = ticket.payment
                  ? ticket.payment.amount_cents
                  : ticket.items?.reduce((sum, item) => sum + item.price_cents, 0) || 0;
                
                // Format date string to extract just date and time parts
                const dt = new Date(ticket.created_at);
                const datePart = new Intl.DateTimeFormat("en-US", { month: "short", day: "numeric", year: "numeric" }).format(dt).toLowerCase();
                const timePart = new Intl.DateTimeFormat("en-US", { hour: "numeric", minute: "2-digit" }).format(dt).toLowerCase();

                return (
                  <tr key={ticket.id}>
                    <td className={styles.ticketIdCell}>
                      <strong>
                        {ticket.display_ticket_number || ticket.local_ticket_id.split("-")[0]}
                      </strong>
                    </td>
                    <td>
                      <span className={styles.dateText}>{datePart}</span>
                      <span className={styles.timeText}>{timePart.replace('am', 'a.m.').replace('pm', 'p.m.')}</span>
                    </td>
                    <td>{ticket.customer_name || "Walk-in customer"}</td>
                    <td>
                      {ticket.barber ? (
                        <div className={styles.barberCell}>
                          <span className={styles.avatar}>{(ticket.barber.display_name || "B").charAt(0).toUpperCase()}</span>
                          {ticket.barber.display_name || "Unknown Barber"}
                        </div>
                      ) : (
                        <div className={styles.barberCell}>
                          <span className={styles.avatarUnassigned}>U</span>
                          Unassigned
                        </div>
                      )}
                    </td>
                    <td>{serviceName}</td>
                    <td>{totalAmount > 0 ? formatCurrency(totalAmount) : "-"}</td>
                    <td>
                      {ticket.payment ? ticket.payment.payment_method.toUpperCase() : "-"}
                    </td>
                    <td>
                      <span className={`${styles.statusBadge} ${styles[ticket.status] || ""}`}>
                        {formatStatus(ticket.status)}
                      </span>
                    </td>
                    <td>
                      <button
                        className={styles.actionButton}
                        onClick={() => setSelectedTicket(ticket)}
                        title="View Details"
                      >
                        &middot;&middot;&middot;
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          
          <div className={styles.pagination}>
            <div className={styles.paginationInfo}>
              Showing {(initialData.page - 1) * initialData.pageSize + 1} to{" "}
              {Math.min(initialData.page * initialData.pageSize, initialData.total)} of {initialData.total} entries
            </div>
            <div className={styles.paginationControls}>
              <button
                className={styles.pageButton}
                disabled={initialData.page <= 1}
                onClick={() => handlePageChange(initialData.page - 1)}
              >
                Previous
              </button>
              <button
                className={styles.pageButton}
                disabled={initialData.page >= initialData.totalPages}
                onClick={() => handlePageChange(initialData.page + 1)}
              >
                Next
              </button>
            </div>
          </div>
        </div>
      )}

      {selectedTicket && (
        <TicketDetails ticket={selectedTicket} onClose={() => setSelectedTicket(null)} />
      )}
    </div>
  );
}
