/* eslint-disable @typescript-eslint/no-explicit-any */
"use client";

import { useState, useMemo, useEffect, useRef } from "react";
import { adminCancelAppointment, adminMarkNoShow, adminCompleteAppointment, adminReassignAppointment, adminGetRescheduleSlots, adminRescheduleAppointment } from "@/app/actions/admin-appointments";
import styles from "./admin-appointments.module.css";
import { Search, MoreVertical, CheckCircle2, XCircle, UserMinus, CalendarX, UserCog, CalendarClock, Copy } from "lucide-react";
import { AppointmentQrCode } from "@/components/booking/appointment-qr";

export function AdminAppointmentsTable({ appointments, barbers }: { appointments: any[], barbers: any[] }) {
  const [loading, setLoading] = useState<string | null>(null);
  const [reassigningId, setReassigningId] = useState<string | null>(null);
  const [newBarberId, setNewBarberId] = useState<string>("");
  const [searchTerm, setSearchTerm] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("all");
  const [openDropdownId, setOpenDropdownId] = useState<string | null>(null);
  const [reschedulingAppointment, setReschedulingAppointment] = useState<any | null>(null);
  const [rescheduleDate, setRescheduleDate] = useState("");
  const [rescheduleSlots, setRescheduleSlots] = useState<any[]>([]);
  const [selectedRescheduleSlot, setSelectedRescheduleSlot] = useState<any | null>(null);
  const [rescheduleError, setRescheduleError] = useState<string | null>(null);
  const [rescheduleLoading, setRescheduleLoading] = useState(false);
  const [expandedQrCode, setExpandedQrCode] = useState<string | null>(null);

  const dropdownRef = useRef<HTMLTableSectionElement>(null);

  // Close dropdown on click outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setOpenDropdownId(null);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const handleAction = async (action: () => Promise<any>, id: string) => {
    setLoading(id);
    setOpenDropdownId(null);
    await action();
    setLoading(null);
  };

  const handleReassign = async (id: string) => {
    if (!newBarberId) return;
    setLoading(id);
    const res = await adminReassignAppointment(id, newBarberId);
    if (res.error) alert(res.error);
    setLoading(null);
    setReassigningId(null);
  };

  const loadRescheduleSlots = async (appointmentId: string, date: string) => {
    setRescheduleDate(date);
    setSelectedRescheduleSlot(null);
    setRescheduleSlots([]);
    if (!date) return;

    setRescheduleLoading(true);
    setRescheduleError(null);
    const res = await adminGetRescheduleSlots(appointmentId, date);
    setRescheduleLoading(false);

    if (res.error) {
      setRescheduleError(res.error);
      return;
    }

    setRescheduleSlots(res.slots || []);
  };

  const startReschedule = async (appointment: any) => {
    const initialDate = new Date(appointment.starts_at).toISOString().split("T")[0];
    setOpenDropdownId(null);
    setReschedulingAppointment(appointment);
    setRescheduleError(null);
    await loadRescheduleSlots(appointment.id, initialDate);
  };

  const cancelReschedule = () => {
    setReschedulingAppointment(null);
    setRescheduleDate("");
    setRescheduleSlots([]);
    setSelectedRescheduleSlot(null);
    setRescheduleError(null);
  };

  const confirmReschedule = async () => {
    if (!reschedulingAppointment || !selectedRescheduleSlot) return;

    setRescheduleLoading(true);
    setRescheduleError(null);
    const res = await adminRescheduleAppointment(reschedulingAppointment.id, selectedRescheduleSlot.starts_at);
    setRescheduleLoading(false);

    if (res.error) {
      setRescheduleError(res.error);
      return;
    }

    cancelReschedule();
  };

  const copyAppointmentCode = async (code: string) => {
    try {
      await navigator.clipboard.writeText(code);
    } catch {
      window.prompt("Copy appointment code", code);
    }
  };

  const filteredAppointments = useMemo(() => {
    return appointments.filter(app => {
      const customerName = (app.customer as any)?.display_name?.toLowerCase() || "";
      const customerPhone = (app.customer as any)?.phone?.toLowerCase() || "";
      const matchesSearch = customerName.includes(searchTerm.toLowerCase()) || customerPhone.includes(searchTerm.toLowerCase());
      
      const matchesFilter = statusFilter === "all" || app.status === statusFilter;

      return matchesSearch && matchesFilter;
    });
  }, [appointments, searchTerm, statusFilter]);

  const getInitials = (name: string) => {
    if (!name || name === "Unknown") return "?";
    return name.substring(0, 2).toUpperCase();
  };

  return (
    <div className={styles.container}>
      {loading && (
        <div className={styles.loadingOverlay}>
          <div className={styles.loader}>
            <div className={styles.spinner}></div>
            Processing...
          </div>
        </div>
      )}

      <div className={styles.controls}>
        <div className={styles.searchBox}>
          <Search className={styles.searchIcon} />
          <input 
            type="text" 
            placeholder="Search by customer name or phone..." 
            className={styles.searchInput}
            value={searchTerm}
            onChange={e => setSearchTerm(e.target.value)}
          />
        </div>

        <div className={styles.filters}>
          <button className={`${styles.filterBtn} ${statusFilter === 'all' ? styles.active : ''}`} onClick={() => setStatusFilter('all')}>All</button>
          <button className={`${styles.filterBtn} ${statusFilter === 'confirmed' ? styles.active : ''}`} onClick={() => setStatusFilter('confirmed')}>Confirmed</button>
          <button className={`${styles.filterBtn} ${statusFilter === 'pending' ? styles.active : ''}`} onClick={() => setStatusFilter('pending')}>Pending</button>
          <button className={`${styles.filterBtn} ${statusFilter === 'completed' ? styles.active : ''}`} onClick={() => setStatusFilter('completed')}>Completed</button>
          <button className={`${styles.filterBtn} ${statusFilter === 'cancelled' ? styles.active : ''}`} onClick={() => setStatusFilter('cancelled')}>Cancelled</button>
        </div>
      </div>

      <div className={styles.card}>
        <div className={styles.tableWrapper}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>Customer</th>
                <th>Service & Date</th>
                <th>Barber</th>
                <th>Status</th>
                <th style={{ textAlign: "right" }}>Actions</th>
              </tr>
            </thead>
            <tbody ref={dropdownRef}>
              {filteredAppointments.map(app => {
                const date = new Date(app.starts_at);
                const isPendingOrConfirmed = ["pending", "confirmed"].includes(app.status);
                const canReschedule = isPendingOrConfirmed && date > new Date();
                const canMarkNoShow = isPendingOrConfirmed && date.getTime() + 10 * 60000 <= new Date().getTime();
                const customerName = (app.customer as any)?.display_name || "Unknown";

                return (
                  <tr key={app.id}>
                    <td data-label="Customer">
                      <div className={styles.avatarCell}>
                        <div className={styles.avatar}>{getInitials(customerName)}</div>
                        <div className={styles.customerInfo}>
                          <span className={styles.customerName}>{customerName}</span>
                          <span className={styles.customerPhone}>{(app.customer as any)?.phone || "No phone"}</span>
                          {app.appointment_code && (
                            <div className={styles.appointmentCode}>
                              <span>{app.appointment_code}</span>
                              <button
                                className={styles.codeButton}
                                onClick={() => copyAppointmentCode(app.appointment_code)}
                                title="Copy appointment code"
                                type="button"
                              >
                                <Copy size={13} />
                              </button>
                            </div>
                          )}
                        </div>
                        {app.appointment_code && (
                          <button 
                            className={styles.qrButton} 
                            onClick={() => setExpandedQrCode(app.appointment_code)}
                            type="button"
                            title="Expand QR Code"
                          >
                            <AppointmentQrCode className={styles.miniQr} size={56} value={app.appointment_code} />
                          </button>
                        )}
                      </div>
                    </td>
                    <td data-label="Service & Date">
                      <div className={styles.dateTime}>
                        <span className={styles.date}>{(app.service as any)?.name}</span>
                        <span className={styles.time}>
                          {date.toLocaleDateString()} at {date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                        </span>
                      </div>
                    </td>
                    <td data-label="Barber">
                      {reassigningId === app.id ? (
                        <div className={styles.reassignBox}>
                          <select 
                            className={styles.selectInput}
                            value={newBarberId}
                            onChange={(e) => setNewBarberId(e.target.value)}
                          >
                            <option value="" disabled>Select barber...</option>
                            {barbers.map(b => (
                              <option key={b.id} value={b.id}>{b.display_name}</option>
                            ))}
                          </select>
                          <button onClick={() => handleReassign(app.id)} className={styles.primaryAction} style={{ padding: '0.25rem 0.5rem', borderRadius: '4px' }}>OK</button>
                          <button onClick={() => setReassigningId(null)} className={styles.iconBtn}><XCircle size={18} /></button>
                        </div>
                      ) : (
                        <span style={{ fontWeight: 500 }}>{(app.barber as any)?.display_name}</span>
                      )}
                    </td>
                    <td data-label="Status">
                      <span className={`${styles.badge} ${styles[app.status] || ''}`}>
                        {app.status.replace("_", " ")}
                      </span>
                    </td>
                    <td data-label="Actions">
                      <div className={styles.actions}>
                        {app.status === 'confirmed' && (
                          <button 
                            className={`${styles.actionBtn} ${styles.primaryAction}`}
                            onClick={() => { if(confirm('Complete appointment?')) handleAction(() => adminCompleteAppointment(app.id), app.id) }}
                            title="Complete Appointment"
                          >
                            <CheckCircle2 size={16} /> Complete
                          </button>
                        )}
                        
                        {isPendingOrConfirmed && (
                          <div className={styles.dropdownWrapper}>
                            <button 
                              className={styles.iconBtn} 
                              onClick={() => setOpenDropdownId(openDropdownId === app.id ? null : app.id)}
                            >
                              <MoreVertical size={20} />
                            </button>
                            
                            {openDropdownId === app.id && (
                              <div className={styles.dropdownMenu}>
                                {!reassigningId && (
                                  <button 
                                    className={styles.dropdownItem} 
                                    onClick={() => { 
                                      setReassigningId(app.id); 
                                      setNewBarberId(app.barber.id); 
                                      setOpenDropdownId(null);
                                    }}
                                  >
                                    <UserCog size={16} /> Reassign Barber
                                  </button>
                                )}

                                {canReschedule && (
                                  <button
                                    className={styles.dropdownItem}
                                    onClick={() => startReschedule(app)}
                                  >
                                    <CalendarClock size={16} /> Reschedule
                                  </button>
                                )}
                                
                                {canMarkNoShow && (
                                  <button 
                                    className={`${styles.dropdownItem} ${styles.danger}`}
                                    onClick={() => { if(confirm('Mark as No Show?')) handleAction(() => adminMarkNoShow(app.id), app.id) }}
                                  >
                                    <UserMinus size={16} /> Mark No Show
                                  </button>
                                )}

                                <button 
                                  className={`${styles.dropdownItem} ${styles.danger}`}
                                  onClick={() => { if(confirm('Cancel appointment?')) handleAction(() => adminCancelAppointment(app.id), app.id) }}
                                >
                                  <CalendarX size={16} /> Cancel Appointment
                                </button>
                              </div>
                            )}
                          </div>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        
        {filteredAppointments.length === 0 && (
          <div className={styles.emptyState}>
            <CalendarX className={styles.emptyIcon} />
            <h3 className={styles.emptyTitle}>No appointments found</h3>
            <p>Try adjusting your search or filters to find what you are looking for.</p>
          </div>
        )}
      </div>

      {reschedulingAppointment && (
        <div className={styles.modalOverlay} role="dialog" aria-modal="true" aria-label="Reschedule appointment">
          <div className={styles.modal}>
            <div className={styles.modalHeader}>
              <div>
                <h3>Reschedule appointment</h3>
                <p>
                  {(reschedulingAppointment.service as any)?.name} with {(reschedulingAppointment.barber as any)?.display_name}
                </p>
              </div>
              <button className={styles.iconBtn} onClick={cancelReschedule} type="button">
                <XCircle size={20} />
              </button>
            </div>

            <label className={styles.modalLabel}>
              Date
              <input
                className={styles.dateInput}
                min={new Date().toISOString().split("T")[0]}
                onChange={(event) => loadRescheduleSlots(reschedulingAppointment.id, event.target.value)}
                type="date"
                value={rescheduleDate}
              />
            </label>

            {rescheduleError && <div className={styles.formError}>{rescheduleError}</div>}

            <div className={styles.slotGrid}>
              {rescheduleLoading ? (
                <div className={styles.slotPlaceholder}>Loading available times...</div>
              ) : rescheduleSlots.length > 0 ? (
                rescheduleSlots.map((slot) => {
                  const selected = selectedRescheduleSlot?.starts_at === slot.starts_at;
                  const slotDate = new Date(slot.starts_at);
                  return (
                    <button
                      className={`${styles.slotButton} ${selected ? styles.slotButtonActive : ""}`}
                      key={`${slot.barber_id}-${slot.starts_at}`}
                      onClick={() => setSelectedRescheduleSlot(slot)}
                      type="button"
                    >
                      {slotDate.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                    </button>
                  );
                })
              ) : (
                <div className={styles.slotPlaceholder}>No available times for this date.</div>
              )}
            </div>

            <div className={styles.modalActions}>
              <button className={styles.secondaryAction} onClick={cancelReschedule} type="button">
                Cancel
              </button>
              <button
                className={styles.primaryAction}
                disabled={!selectedRescheduleSlot || rescheduleLoading}
                onClick={confirmReschedule}
                type="button"
              >
                {rescheduleLoading ? "Saving..." : "Save new time"}
              </button>
            </div>
          </div>
        </div>
      )}

      {expandedQrCode && (
        <div className={styles.modalOverlay} role="dialog" aria-modal="true" aria-label="Expanded QR Code" onClick={() => setExpandedQrCode(null)}>
          <div className={styles.qrModalContent} onClick={e => e.stopPropagation()}>
            <div className={styles.qrModalHeader}>
              <h3 style={{ margin: 0, fontSize: '1.125rem', color: 'var(--text)' }}>Appointment Code</h3>
              <button className={styles.iconBtn} onClick={() => setExpandedQrCode(null)} type="button">
                <XCircle size={24} />
              </button>
            </div>
            <div className={styles.qrModalBody}>
              <AppointmentQrCode size={256} value={expandedQrCode} />
              <div className={styles.qrCodeTextLarge}>{expandedQrCode}</div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
