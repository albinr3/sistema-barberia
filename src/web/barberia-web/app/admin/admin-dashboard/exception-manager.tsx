"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/ui/status-badge";
import type { AvailabilityExceptionRow, BarberRow } from "@/lib/catalog/types";
import { deleteAvailabilityException, saveAvailabilityException } from "./actions";
import styles from "./catalog.module.css";

function isoDate(offsetDays = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offsetDays);
  return date.toISOString().slice(0, 10);
}

function ExceptionForm({
  barbers,
  exception,
  onCancel,
}: {
  barbers: BarberRow[];
  exception?: AvailabilityExceptionRow;
  onCancel: () => void;
}) {
  return (
    <form action={saveAvailabilityException} className={styles.inlineForm}>
      <input name="id" type="hidden" value={exception?.id ?? ""} />
      <div className={styles.formHelp}>
        ℹ️ <strong>How exceptions work:</strong>
        <ul>
          <li><strong>Custom Hours:</strong> Setting a <strong>Start</strong> and <strong>End</strong> time replaces the normal schedule for this date. <strong>Only</strong> the hours between these times will be available for booking.</li>
          <li><strong>Fully Closed:</strong> Checking <strong>Closed</strong> makes the barber completely unavailable for the entire day.</li>
        </ul>
      </div>
      <label>
        Barber
        <select name="barber_id" required defaultValue={exception?.barber_id ?? ""}>
          <option value="" disabled>
            Select barber
          </option>
          {barbers.map((barber) => (
            <option key={barber.id} value={barber.id}>
              {barber.display_name}
            </option>
          ))}
        </select>
      </label>
      <label>
        Date
        <input name="exception_date" required type="date" defaultValue={exception?.exception_date ?? isoDate()} />
      </label>
      <label>
        Start
        <input name="starts_at" type="time" defaultValue={exception?.starts_at?.slice(0, 5) ?? ""} />
      </label>
      <label>
        End
        <input name="ends_at" type="time" defaultValue={exception?.ends_at?.slice(0, 5) ?? ""} />
      </label>
      <label>
        Reason
        <input name="reason" type="text" defaultValue={exception?.reason ?? ""} />
      </label>
      <label className={styles.checkboxLabel}>
        <input name="is_closed" type="checkbox" defaultChecked={exception?.is_closed ?? false} />
        Closed
      </label>
      <div className={styles.formActions}>
        <Button type="submit" variant="primary">
          {exception ? "Save changes" : "Add exception"}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

function barberName(barbers: BarberRow[], id: string) {
  return barbers.find((barber) => barber.id === id)?.display_name ?? "Unknown barber";
}

export function ExceptionManager({
  barbers,
  exceptions,
}: {
  barbers: BarberRow[];
  exceptions: AvailabilityExceptionRow[];
}) {
  const [editingId, setEditingId] = useState<string | null>(null);
  const [isAdding, setIsAdding] = useState(false);

  if (barbers.length === 0) {
    return <p className={styles.empty}>Create a barber before adding date exceptions.</p>;
  }

  return (
    <div className={styles.managerContainer}>
      <div className={styles.managerHeader}>
        <Button
          type="button"
          variant="secondary"
          onClick={() => {
            setIsAdding(true);
            setEditingId(null);
          }}
          disabled={isAdding}
        >
          + Add new exception
        </Button>
      </div>

      {isAdding && (
        <div className={styles.editPanel}>
          <h3 className={styles.editPanelTitle}>New Exception</h3>
          <ExceptionForm barbers={barbers} onCancel={() => setIsAdding(false)} />
        </div>
      )}

      {exceptions.length === 0 && !isAdding ? (
        <p className={styles.empty}>No date exceptions yet.</p>
      ) : (
        <div className={styles.dataList}>
          {exceptions.map((exception) => (
            <div
              key={exception.id}
              className={`${styles.dataRow} ${editingId === exception.id ? styles.dataRowActive : ""}`}
            >
              <div className={styles.rowSummary}>
                <div className={styles.rowInfo}>
                  <strong>{barberName(barbers, exception.barber_id)}</strong>
                  <span>
                    {exception.exception_date}{" "}
                    {exception.is_closed
                      ? "closed"
                      : `${exception.starts_at?.slice(0, 5)}-${exception.ends_at?.slice(0, 5)}`}
                  </span>
                </div>
                <div className={styles.rowActions}>
                  <StatusBadge tone={exception.is_closed ? "danger" : "warning"}>
                    {exception.is_closed ? "Closed" : "Custom"}
                  </StatusBadge>
                  <Button
                    type="button"
                    variant="ghost"
                    onClick={() => {
                      setEditingId(editingId === exception.id ? null : exception.id);
                      setIsAdding(false);
                    }}
                  >
                    {editingId === exception.id ? "Close" : "Edit"}
                  </Button>
                </div>
              </div>

              {editingId === exception.id && (
                <div className={styles.editPanel}>
                  <ExceptionForm barbers={barbers} exception={exception} onCancel={() => setEditingId(null)} />
                  <div className={styles.deleteSection}>
                    <form action={deleteAvailabilityException}>
                      <input name="id" type="hidden" value={exception.id} />
                      <Button type="submit" variant="ghost">
                        Delete exception
                      </Button>
                    </form>
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
