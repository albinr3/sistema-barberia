import { describe, expect, it } from "vitest";
import { buildTicketsDashboardSnapshot, formatTicketNumber, type TicketDashboardBarberRow, type TicketDashboardTicketRow } from "./tickets-dashboard";

const baseTicket = {
  id: "ticket-id",
  local_ticket_id: "W20260615090000000",
  display_ticket_number: 1,
  ticket_date: "2026-06-15",
  customer_name: "Customer",
  barber_id: null,
  created_at: "2026-06-15T13:00:00.000Z",
  checked_in_at: "2026-06-15T13:00:00.000Z",
  updated_at: "2026-06-15T13:00:00.000Z",
  barber: null,
} satisfies Omit<TicketDashboardTicketRow, "status">;

const baseBarber = {
  id: "barber-1",
  display_name: "Alfredo",
  station_code: "B-1",
  is_active: true,
  is_available_locally: true,
} satisfies TicketDashboardBarberRow;

describe("tickets dashboard snapshot", () => {
  it("separates called and waiting tickets by arrival", () => {
    const snapshot = buildTicketsDashboardSnapshot({
      loadedAt: new Date("2026-06-15T13:10:00.000Z"),
      barbers: [baseBarber],
      tickets: [
        { ...baseTicket, id: "waiting-2", display_ticket_number: 3, status: "waiting", checked_in_at: "2026-06-15T13:03:00.000Z" },
        { ...baseTicket, id: "called-1", display_ticket_number: 2, barber_id: "barber-1", status: "called", checked_in_at: "2026-06-15T13:02:00.000Z" },
        { ...baseTicket, id: "waiting-1", display_ticket_number: 1, status: "waiting", checked_in_at: "2026-06-15T13:01:00.000Z" },
      ],
      devices: [{ id: "device-1", name: "Desktop", last_sync_at: "2026-06-15T13:09:00.000Z" }],
    });

    expect(snapshot.nowCalling.map((ticket) => ticket.id)).toEqual(["called-1"]);
    expect(snapshot.waiting.map((ticket) => ticket.id)).toEqual(["waiting-1", "waiting-2"]);
    expect(snapshot.activeQueue.map((ticket) => ticket.id)).toEqual(["waiting-1", "called-1", "waiting-2"]);
    expect(snapshot.waitingTotal).toBe(2);
    expect(snapshot.isStale).toBe(false);
  });

  it("calculates alerts correctly", () => {
    const snapshot = buildTicketsDashboardSnapshot({
      loadedAt: new Date("2026-06-15T13:40:00.000Z"),
      barbers: [baseBarber],
      tickets: [
        { ...baseTicket, id: "waiting-ok", display_ticket_number: 1, status: "waiting", checked_in_at: "2026-06-15T13:20:00.000Z", updated_at: "2026-06-15T13:20:00.000Z" },
        { ...baseTicket, id: "waiting-alert", display_ticket_number: 2, status: "waiting", checked_in_at: "2026-06-15T13:00:00.000Z", updated_at: "2026-06-15T13:00:00.000Z" },
        { ...baseTicket, id: "called-alert", display_ticket_number: 3, status: "called", updated_at: "2026-06-15T13:35:00.000Z" },
      ],
    });

    expect(snapshot.alerts.map((alert) => [alert.ticketId, alert.type])).toEqual([
      ["waiting-alert", "waiting_too_long"],
      ["called-alert", "called_too_long"],
    ]);
  });

  it("uses the visible ticket number before the internal ticket id", () => {
    expect(formatTicketNumber({ ...baseTicket, status: "waiting", display_ticket_number: 7 })).toBe("7");
    expect(formatTicketNumber({ ...baseTicket, status: "waiting", display_ticket_number: null })).toBe("W20260615090000000");
  });

  it("infers barber display state from active tickets", () => {
    const snapshot = buildTicketsDashboardSnapshot({
      loadedAt: new Date("2026-06-15T13:10:00.000Z"),
      barbers: [baseBarber, { ...baseBarber, id: "barber-2", display_name: "Julio", station_code: "B-2" }],
      tickets: [
        { ...baseTicket, id: "busy", display_ticket_number: 4, barber_id: "barber-2", status: "in_progress" },
        { ...baseTicket, id: "called", display_ticket_number: 5, barber_id: "barber-1", status: "called" },
      ],
    });

    expect(snapshot.barbers.map((barber) => [barber.id, barber.status, barber.detail])).toEqual([
      ["barber-1", "calling", "Calling: 5"],
      ["barber-2", "busy", "Serving: 4"],
    ]);
  });
});
