"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/ui/status-badge";
import { formatPriceFromCents } from "@/lib/catalog/filters";
import type { ServiceRow } from "@/lib/catalog/types";
import { saveService } from "./actions";
import styles from "./catalog.module.css";

function ServiceForm({ service, onCancel }: { service?: ServiceRow; onCancel: () => void }) {
  return (
    <form action={saveService} className={styles.inlineForm}>
      <input name="id" type="hidden" value={service?.id ?? ""} />
      <label>
        Name
        <input name="name" required type="text" defaultValue={service?.name ?? ""} />
      </label>
      <label>
        Description
        <input name="description" type="text" defaultValue={service?.description ?? ""} />
      </label>
      <label>
        Price
        <input
          name="base_price"
          required
          type="text"
          inputMode="decimal"
          defaultValue={service ? (service.base_price_cents / 100).toFixed(2) : ""}
        />
      </label>
      <label>
        Minutes
        <input
          name="duration_minutes"
          required
          min={1}
          type="number"
          defaultValue={service?.duration_minutes ?? 30}
        />
      </label>
      <label>
        Order
        <input name="sort_order" required type="number" defaultValue={service?.sort_order ?? 0} />
      </label>
      <label className={styles.checkboxLabel}>
        <input name="is_active" type="checkbox" defaultChecked={service?.is_active ?? true} />
        Active
      </label>
      <div className={styles.formActions}>
        <Button type="submit" variant="primary">
          {service ? "Save changes" : "Add service"}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

export function ServiceManager({ services }: { services: ServiceRow[] }) {
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
          + Add new service
        </Button>
      </div>

      {isAdding && (
        <div className={styles.editPanel}>
          <h3 className={styles.editPanelTitle}>New Service</h3>
          <ServiceForm onCancel={() => setIsAdding(false)} />
        </div>
      )}

      {services.length === 0 && !isAdding ? (
        <p className={styles.empty}>No services yet.</p>
      ) : (
        <div className={styles.dataList}>
          {services.map((service) => (
            <div
              key={service.id}
              className={`${styles.dataRow} ${editingId === service.id ? styles.dataRowActive : ""}`}
            >
              <div className={styles.rowSummary}>
                <div className={styles.rowInfo}>
                  <strong>{service.name}</strong>
                  <span>
                    {formatPriceFromCents(service.base_price_cents)} / {service.duration_minutes} min
                  </span>
                </div>
                <div className={styles.rowActions}>
                  <StatusBadge tone={service.is_active ? "success" : "neutral"}>
                    {service.is_active ? "Active" : "Inactive"}
                  </StatusBadge>
                  <Button
                    type="button"
                    variant="ghost"
                    onClick={() => {
                      setEditingId(editingId === service.id ? null : service.id);
                      setIsAdding(false);
                    }}
                  >
                    {editingId === service.id ? "Close" : "Edit"}
                  </Button>
                </div>
              </div>

              {editingId === service.id && (
                <div className={styles.editPanel}>
                  <ServiceForm service={service} onCancel={() => setEditingId(null)} />
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
