import { AppShell } from "@/components/layout/app-shell";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/ui/status-badge";
import { requireAdmin } from "@/lib/auth/profile";
import { activeOnly, formatPriceFromCents } from "@/lib/catalog/filters";
import { getAdminCatalogData, getAvailabilityPreview } from "@/lib/catalog/queries";
import type {
  AvailabilityExceptionRow,
  AvailabilityRuleRow,
  BarberRow,
  BarberServiceRow,
  ServiceRow,
} from "@/lib/catalog/types";
import { createClient } from "@/lib/supabase/server";
import {
  deleteAvailabilityException,
  deleteAvailabilityRule,
  saveAvailabilityException,
  saveAvailabilityRule,
  saveBarber,
  saveBarberService,
  saveService,
} from "./actions";
import styles from "./catalog.module.css";

const dayNames = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

type CatalogPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function firstParam(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function barberName(barbers: BarberRow[], id: string) {
  return barbers.find((barber) => barber.id === id)?.display_name ?? "Unknown barber";
}

function serviceName(services: ServiceRow[], id: string) {
  return services.find((service) => service.id === id)?.name ?? "Unknown service";
}

function isoDate(offsetDays = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offsetDays);
  return date.toISOString().slice(0, 10);
}

function Section({
  title,
  description,
  children,
}: {
  title: string;
  description: string;
  children: React.ReactNode;
}) {
  return (
    <section className={styles.section}>
      <div className={styles.sectionHeader}>
        <div>
          <h2>{title}</h2>
          <p>{description}</p>
        </div>
      </div>
      {children}
    </section>
  );
}

function StatusMessage({ success, error }: { success?: string; error?: string }) {
  if (!success && !error) {
    return null;
  }

  return (
    <div className={error ? styles.error : styles.notice} role="status">
      {error ?? success}
    </div>
  );
}

function EmptyState({ children }: { children: React.ReactNode }) {
  return <p className={styles.empty}>{children}</p>;
}

function BarberForm({ barber }: { barber?: BarberRow }) {
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
      <Button type="submit" variant={barber ? "secondary" : "primary"}>
        {barber ? "Save" : "Add barber"}
      </Button>
    </form>
  );
}

function ServiceForm({ service }: { service?: ServiceRow }) {
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
      <Button type="submit" variant={service ? "secondary" : "primary"}>
        {service ? "Save" : "Add service"}
      </Button>
    </form>
  );
}

function AssignmentForm({
  barbers,
  services,
  assignment,
}: {
  barbers: BarberRow[];
  services: ServiceRow[];
  assignment?: BarberServiceRow;
}) {
  return (
    <form action={saveBarberService} className={styles.inlineForm}>
      <label>
        Barber
        <select name="barber_id" required defaultValue={assignment?.barber_id ?? ""}>
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
        Service
        <select name="service_id" required defaultValue={assignment?.service_id ?? ""}>
          <option value="" disabled>
            Select service
          </option>
          {services.map((service) => (
            <option key={service.id} value={service.id}>
              {service.name}
            </option>
          ))}
        </select>
      </label>
      <label className={styles.checkboxLabel}>
        <input name="is_active" type="checkbox" defaultChecked={assignment?.is_active ?? true} />
        Active
      </label>
      <Button type="submit" variant={assignment ? "secondary" : "primary"}>
        {assignment ? "Save" : "Assign"}
      </Button>
    </form>
  );
}

function RuleForm({ barbers, rule }: { barbers: BarberRow[]; rule?: AvailabilityRuleRow }) {
  return (
    <form action={saveAvailabilityRule} className={styles.inlineForm}>
      <input name="id" type="hidden" value={rule?.id ?? ""} />
      <label>
        Barber
        <select name="barber_id" required defaultValue={rule?.barber_id ?? ""}>
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
        Day
        <select name="day_of_week" required defaultValue={rule?.day_of_week ?? 1}>
          {dayNames.map((day, index) => (
            <option key={day} value={index}>
              {day}
            </option>
          ))}
        </select>
      </label>
      <label>
        Start
        <input name="starts_at" required type="time" defaultValue={rule?.starts_at?.slice(0, 5) ?? "09:00"} />
      </label>
      <label>
        End
        <input name="ends_at" required type="time" defaultValue={rule?.ends_at?.slice(0, 5) ?? "17:00"} />
      </label>
      <label>
        Slot
        <input name="slot_minutes" required min={1} type="number" defaultValue={rule?.slot_minutes ?? 30} />
      </label>
      <label className={styles.checkboxLabel}>
        <input name="is_active" type="checkbox" defaultChecked={rule?.is_active ?? true} />
        Active
      </label>
      <Button type="submit" variant={rule ? "secondary" : "primary"}>
        {rule ? "Save" : "Add rule"}
      </Button>
    </form>
  );
}

function ExceptionForm({
  barbers,
  exception,
}: {
  barbers: BarberRow[];
  exception?: AvailabilityExceptionRow;
}) {
  return (
    <form action={saveAvailabilityException} className={styles.inlineForm}>
      <input name="id" type="hidden" value={exception?.id ?? ""} />
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
      <Button type="submit" variant={exception ? "secondary" : "primary"}>
        {exception ? "Save" : "Add exception"}
      </Button>
    </form>
  );
}

export default async function AdminCatalogPage({ searchParams }: CatalogPageProps) {
  const params = await searchParams;
  const supabase = await createClient();
  await requireAdmin(supabase);
  const data = await getAdminCatalogData(supabase);
  const activeBarbers = activeOnly(data.barbers);
  const activeServices = activeOnly(data.services);
  const previewServiceId = firstParam(params.previewServiceId) ?? activeServices[0]?.id ?? "";
  const previewBarberId = firstParam(params.previewBarberId) ?? "";
  const previewStartsOn = firstParam(params.previewStartsOn) ?? isoDate();
  const previewEndsOn = firstParam(params.previewEndsOn) ?? isoDate(6);
  const previewSlots =
    previewServiceId && previewStartsOn && previewEndsOn
      ? await getAvailabilityPreview(supabase, {
          serviceId: previewServiceId,
          startsOn: previewStartsOn,
          endsOn: previewEndsOn,
          barberId: previewBarberId || null,
        })
      : [];

  return (
    <AppShell title="Catalog" variant="admin">
      <div className={styles.page}>
        <StatusMessage success={firstParam(params.success)} error={firstParam(params.error)} />

        <Section
          title="Barbers"
          description="Manage the web catalog authority for active barbers, stations and image paths."
        >
          <BarberForm />
          {data.barbers.length === 0 ? (
            <EmptyState>No barbers yet.</EmptyState>
          ) : (
            <div className={styles.stack}>
              {data.barbers.map((barber) => (
                <div className={styles.rowPanel} key={barber.id}>
                  <div className={styles.rowSummary}>
                    <strong>{barber.display_name}</strong>
                    <span>{barber.station_code ?? "No station"}</span>
                    <StatusBadge tone={barber.is_active ? "success" : "neutral"}>
                      {barber.is_active ? "Active" : "Inactive"}
                    </StatusBadge>
                  </div>
                  <BarberForm barber={barber} />
                </div>
              ))}
            </div>
          )}
        </Section>

        <Section title="Services" description="Control prices, duration, display order and service visibility.">
          <ServiceForm />
          {data.services.length === 0 ? (
            <EmptyState>No services yet.</EmptyState>
          ) : (
            <div className={styles.stack}>
              {data.services.map((service) => (
                <div className={styles.rowPanel} key={service.id}>
                  <div className={styles.rowSummary}>
                    <strong>{service.name}</strong>
                    <span>
                      {formatPriceFromCents(service.base_price_cents)} / {service.duration_minutes} min
                    </span>
                    <StatusBadge tone={service.is_active ? "success" : "neutral"}>
                      {service.is_active ? "Active" : "Inactive"}
                    </StatusBadge>
                  </div>
                  <ServiceForm service={service} />
                </div>
              ))}
            </div>
          )}
        </Section>

        <Section title="Barber services" description="Choose which active services each barber can be booked for.">
          {data.barbers.length === 0 || data.services.length === 0 ? (
            <EmptyState>Create at least one barber and one service before assigning services.</EmptyState>
          ) : (
            <AssignmentForm barbers={data.barbers} services={data.services} />
          )}
          {data.barberServices.length === 0 ? (
            <EmptyState>No service assignments yet.</EmptyState>
          ) : (
            <div className={styles.stack}>
              {data.barberServices.map((assignment) => (
                <div className={styles.rowPanel} key={`${assignment.barber_id}-${assignment.service_id}`}>
                  <div className={styles.rowSummary}>
                    <strong>{barberName(data.barbers, assignment.barber_id)}</strong>
                    <span>{serviceName(data.services, assignment.service_id)}</span>
                    <StatusBadge tone={assignment.is_active ? "success" : "neutral"}>
                      {assignment.is_active ? "Active" : "Inactive"}
                    </StatusBadge>
                  </div>
                  <AssignmentForm barbers={data.barbers} services={data.services} assignment={assignment} />
                </div>
              ))}
            </div>
          )}
        </Section>

        <Section title="Weekly availability" description="Define recurring local business hours by barber.">
          {data.barbers.length === 0 ? <EmptyState>Create a barber before adding availability.</EmptyState> : <RuleForm barbers={data.barbers} />}
          {data.availabilityRules.length === 0 ? (
            <EmptyState>No weekly rules yet.</EmptyState>
          ) : (
            <div className={styles.stack}>
              {data.availabilityRules.map((rule) => (
                <div className={styles.rowPanel} key={rule.id}>
                  <div className={styles.rowSummary}>
                    <strong>{barberName(data.barbers, rule.barber_id)}</strong>
                    <span>
                      {dayNames[rule.day_of_week]} {rule.starts_at.slice(0, 5)}-{rule.ends_at.slice(0, 5)}
                    </span>
                    <StatusBadge tone={rule.is_active ? "success" : "neutral"}>
                      {rule.is_active ? "Active" : "Inactive"}
                    </StatusBadge>
                  </div>
                  <RuleForm barbers={data.barbers} rule={rule} />
                  <form action={deleteAvailabilityRule}>
                    <input name="id" type="hidden" value={rule.id} />
                    <Button type="submit" variant="ghost">
                      Delete rule
                    </Button>
                  </form>
                </div>
              ))}
            </div>
          )}
        </Section>

        <Section title="Date exceptions" description="Close a day or replace normal hours for a specific date.">
          {data.barbers.length === 0 ? (
            <EmptyState>Create a barber before adding date exceptions.</EmptyState>
          ) : (
            <ExceptionForm barbers={data.barbers} />
          )}
          {data.availabilityExceptions.length === 0 ? (
            <EmptyState>No date exceptions yet.</EmptyState>
          ) : (
            <div className={styles.stack}>
              {data.availabilityExceptions.map((exception) => (
                <div className={styles.rowPanel} key={exception.id}>
                  <div className={styles.rowSummary}>
                    <strong>{barberName(data.barbers, exception.barber_id)}</strong>
                    <span>
                      {exception.exception_date}{" "}
                      {exception.is_closed
                        ? "closed"
                        : `${exception.starts_at?.slice(0, 5)}-${exception.ends_at?.slice(0, 5)}`}
                    </span>
                    <StatusBadge tone={exception.is_closed ? "danger" : "warning"}>
                      {exception.is_closed ? "Closed" : "Custom"}
                    </StatusBadge>
                  </div>
                  <ExceptionForm barbers={data.barbers} exception={exception} />
                  <form action={deleteAvailabilityException}>
                    <input name="id" type="hidden" value={exception.id} />
                    <Button type="submit" variant="ghost">
                      Delete exception
                    </Button>
                  </form>
                </div>
              ))}
            </div>
          )}
        </Section>

        <Section title="Availability preview" description="Preview the RPC contract that booking will consume in 2.3.">
          <form className={styles.inlineForm} method="get">
            <label>
              Service
              <select name="previewServiceId" required defaultValue={previewServiceId}>
                {activeServices.map((service) => (
                  <option key={service.id} value={service.id}>
                    {service.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Barber
              <select name="previewBarberId" defaultValue={previewBarberId}>
                <option value="">Any active barber</option>
                {activeBarbers.map((barber) => (
                  <option key={barber.id} value={barber.id}>
                    {barber.display_name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              From
              <input name="previewStartsOn" required type="date" defaultValue={previewStartsOn} />
            </label>
            <label>
              To
              <input name="previewEndsOn" required type="date" defaultValue={previewEndsOn} />
            </label>
            <Button type="submit" variant="secondary">
              Preview
            </Button>
          </form>
          {previewSlots.length === 0 ? (
            <EmptyState>No available slots for this selection.</EmptyState>
          ) : (
            <div className={styles.slotGrid}>
              {previewSlots.slice(0, 36).map((slot) => (
                <div className={styles.slot} key={`${slot.barber_id}-${slot.starts_at}`}>
                  <strong>{slot.barber_name}</strong>
                  <span>
                    {new Date(slot.starts_at).toLocaleString("en-US", {
                      dateStyle: "medium",
                      timeStyle: "short",
                      timeZone: "America/New_York",
                    })}
                  </span>
                </div>
              ))}
            </div>
          )}
        </Section>
      </div>
    </AppShell>
  );
}
