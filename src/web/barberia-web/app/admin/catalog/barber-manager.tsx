"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/ui/status-badge";
import type { BarberRow } from "@/lib/catalog/types";
import { saveBarber } from "./actions";
import styles from "./catalog.module.css";

function BarberForm({ barber, onCancel }: { barber?: BarberRow; onCancel: () => void }) {
  return (
    <form action={saveBarber} className={styles.inlineForm}>
      <input name="id" type="hidden" value={barber?.id ?? ""} />
      <label>
        Name
        <input name="display_name" required type="text" defaultValue={barber?.display_name ?? ""} />
      </label>
      <label>
        Station
        <input name="station_code" placeholder="B-1" type="text" defaultValue={barber?.station_code ?? ""} />
      </label>
      <label>
        Image path
        <input
          name="profile_image_path"
          placeholder="storage/path.jpg"
          type="text"
          defaultValue={barber?.profile_image_path ?? ""}
        />
      </label>
      <label className={styles.checkboxLabel}>
        <input name="is_active" type="checkbox" defaultChecked={barber?.is_active ?? true} />
        Active
      </label>
      <div className={styles.formActions}>
        <Button type="submit" variant="primary">
          {barber ? "Save changes" : "Add barber"}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

export function BarberManager({ barbers }: { barbers: BarberRow[] }) {
  const [editingId, setEditingId] = useState<string | null>(null);
  const [isAdding, setIsAdding] = useState(false);

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
          + Add new barber
        </Button>
      </div>

      {isAdding && (
        <div className={styles.editPanel}>
          <h3 className={styles.editPanelTitle}>New Barber</h3>
          <BarberForm onCancel={() => setIsAdding(false)} />
        </div>
      )}

      {barbers.length === 0 && !isAdding ? (
        <p className={styles.empty}>No barbers yet.</p>
      ) : (
        <div className={styles.dataList}>
          {barbers.map((barber) => (
            <div
              key={barber.id}
              className={`${styles.dataRow} ${editingId === barber.id ? styles.dataRowActive : ""}`}
            >
              <div className={styles.rowSummary}>
                <div className={styles.rowInfo}>
                  <strong>{barber.display_name}</strong>
                  <span>{barber.station_code ?? "No station"}</span>
                </div>
                <div className={styles.rowActions}>
                  <StatusBadge tone={barber.is_active ? "success" : "neutral"}>
                    {barber.is_active ? "Active" : "Inactive"}
                  </StatusBadge>
                  <Button
                    type="button"
                    variant="ghost"
                    onClick={() => {
                      setEditingId(editingId === barber.id ? null : barber.id);
                      setIsAdding(false);
                    }}
                  >
                    {editingId === barber.id ? "Close" : "Edit"}
                  </Button>
                </div>
              </div>

              {editingId === barber.id && (
                <div className={styles.editPanel}>
                  <BarberForm barber={barber} onCancel={() => setEditingId(null)} />
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
