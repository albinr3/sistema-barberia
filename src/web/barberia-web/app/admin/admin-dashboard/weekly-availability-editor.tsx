"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import type { AvailabilityRuleRow, BarberRow } from "@/lib/catalog/types";
import { saveWeeklyAvailability } from "./actions";
import styles from "./catalog.module.css";

const dayNames = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

type DaySchedule = {
  day_of_week: number;
  is_open: boolean;
  starts_at: string;
  ends_at: string;
  slot_minutes: number;
};

export function WeeklyAvailabilityEditor({
  barbers,
  initialRules,
}: {
  barbers: BarberRow[];
  initialRules: AvailabilityRuleRow[];
}) {
  const [selectedBarberId, setSelectedBarberId] = useState<string>(barbers[0]?.id ?? "");
  const [schedule, setSchedule] = useState<DaySchedule[]>([]);
  const [isPending, setIsPending] = useState(false);

  useEffect(() => {
    if (!selectedBarberId) return;

    const barberRules = initialRules.filter((r) => r.barber_id === selectedBarberId);

    const defaultSchedule: DaySchedule[] = dayNames.map((_, index) => {
      const rule = barberRules.find((r) => r.day_of_week === index && r.is_active);
      return {
        day_of_week: index,
        is_open: !!rule,
        starts_at: rule?.starts_at.slice(0, 5) ?? "09:00",
        ends_at: rule?.ends_at.slice(0, 5) ?? "17:00",
        slot_minutes: rule?.slot_minutes ?? 30,
      };
    });
    // eslint-disable-next-line
    setSchedule(defaultSchedule);
  }, [selectedBarberId, initialRules]);

  const handleDayChange = (index: number, changes: Partial<DaySchedule>) => {
    setSchedule((prev) => prev.map((day, i) => (i === index ? { ...day, ...changes } : day)));
  };

  if (barbers.length === 0) {
    return <p className={styles.empty}>Create a barber before adding availability.</p>;
  }

  return (
    <form
      action={async () => {
        setIsPending(true);
        try {
          const formData = new FormData();
          if (!selectedBarberId) {
            alert("No barber selected.");
            return;
          }
          formData.append("barber_id", selectedBarberId);
          formData.append("schedule_json", JSON.stringify(schedule));
          await saveWeeklyAvailability(formData);
        } catch (e: unknown) {
          console.error(e);
        } finally {
          setIsPending(false);
        }
      }}
      className={styles.weeklyEditor}
    >
      <div className={styles.weeklyHeader}>
        <label className={styles.barberSelectLabel}>
          <span>Editing schedule for:</span>
          <select value={selectedBarberId} onChange={(e) => setSelectedBarberId(e.target.value)}>
            {barbers.map((barber) => (
              <option key={barber.id} value={barber.id}>
                {barber.display_name}
              </option>
            ))}
          </select>
        </label>
        <Button type="submit" variant="primary" disabled={isPending}>
          {isPending ? "Saving..." : "Save schedule"}
        </Button>
      </div>

      <div className={styles.scheduleGrid}>
        <div className={styles.scheduleHeaderRow}>
          <div>Day</div>
          <div>Status</div>
          <div>Hours</div>
          <div>Slot (min)</div>
        </div>
        {schedule.map((day, index) => (
          <div className={`${styles.scheduleRow} ${!day.is_open ? styles.scheduleRowClosed : ""}`} key={index}>
            <div className={styles.scheduleDayName}>{dayNames[day.day_of_week]}</div>
            
            <div className={styles.scheduleToggle}>
              <label className={styles.toggleLabel}>
                <input
                  type="checkbox"
                  className={styles.srOnly}
                  checked={day.is_open}
                  onChange={(e) => handleDayChange(index, { is_open: e.target.checked })}
                />
                <div className={`${styles.toggleSwitch} ${day.is_open ? styles.toggleSwitchOn : ""}`}></div>
                <span>{day.is_open ? "Open" : "Closed"}</span>
              </label>
            </div>

            {day.is_open ? (
              <>
                <div className={styles.scheduleTimeInputs}>
                  <input
                    type="time"
                    required
                    value={day.starts_at}
                    onChange={(e) => handleDayChange(index, { starts_at: e.target.value })}
                  />
                  <span>-</span>
                  <input
                    type="time"
                    required
                    value={day.ends_at}
                    onChange={(e) => handleDayChange(index, { ends_at: e.target.value })}
                  />
                </div>
                <div className={styles.scheduleSlotInput}>
                  <input
                    type="number"
                    required
                    min={1}
                    value={day.slot_minutes}
                    onChange={(e) => handleDayChange(index, { slot_minutes: parseInt(e.target.value) || 30 })}
                  />
                </div>
              </>
            ) : (
              <div className={styles.scheduleClosedState} style={{ gridColumn: "span 2" }}>
                <span>Not working on this day</span>
              </div>
            )}
          </div>
        ))}
      </div>
    </form>
  );
}
