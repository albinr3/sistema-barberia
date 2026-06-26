import { AppShell } from "@/components/layout/app-shell";
import { Button } from "@/components/ui/button";
import { requireAdmin } from "@/lib/auth/profile";
import { activeOnly } from "@/lib/catalog/filters";
import { getAdminCatalogData } from "@/lib/catalog/queries";
import { createClient } from "@/lib/supabase/server";
import { BarberManager } from "./barber-manager";
import styles from "./catalog.module.css";
import { ExceptionManager } from "./exception-manager";
import { ServiceManager } from "./service-manager";
import { WeeklyAvailabilityEditor } from "./weekly-availability-editor";

type CatalogPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function firstParam(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
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
  secondary,
  collapsible,
}: {
  title: string;
  description: string;
  children: React.ReactNode;
  secondary?: boolean;
  collapsible?: boolean;
}) {
  if (collapsible) {
    return (
      <details className={`${styles.section} ${styles.collapsibleSection} ${secondary ? styles.sectionSecondary : ""}`}>
        <summary className={styles.sectionHeader}>
          <div>
            <h2>{title}</h2>
            <p>{description}</p>
          </div>
        </summary>
        <div className={styles.sectionContent}>
          {children}
        </div>
      </details>
    );
  }

  return (
    <section className={`${styles.section} ${secondary ? styles.sectionSecondary : ""}`}>
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

export default async function AdminCatalogPage({ searchParams }: CatalogPageProps) {
  const params = await searchParams;
  const supabase = await createClient();
  await requireAdmin(supabase);
  const data = await getAdminCatalogData(supabase);

  const activeServices = activeOnly(data.services);

  const metrics = {
    activeBarbers: activeOnly(data.barbers).length,
    activeServices: activeServices.length,
    activeRules: data.availabilityRules.filter((r) => r.is_active).length,
    upcomingExceptions: data.availabilityExceptions.length,
  };

  return (
    <AppShell title="Admin Dashboard" variant="admin">
      <div className={styles.page}>
        <StatusMessage success={firstParam(params.success)} error={firstParam(params.error)} />

        <div className={styles.summaryGrid}>
          <div className={styles.summaryCard}>
            <span>Active Barbers</span>
            <strong>{metrics.activeBarbers}</strong>
          </div>
          <div className={styles.summaryCard}>
            <span>Active Services</span>
            <strong>{metrics.activeServices}</strong>
          </div>
          <div className={styles.summaryCard}>
            <span>Weekly Rules</span>
            <strong>{metrics.activeRules}</strong>
          </div>
          <div className={styles.summaryCard}>
            <span>Upcoming Exceptions</span>
            <strong>{metrics.upcomingExceptions}</strong>
          </div>
        </div>

        <Section
          title="Barbers"
          description="Manage the web catalog authority for active barbers, stations, image paths and kiosk selection."
          collapsible
        >
          <BarberManager barbers={data.barbers} />
        </Section>

        <Section title="Services" description="Control prices, duration, display order and service visibility." collapsible>
          <ServiceManager services={data.services} />
        </Section>

        <Section title="Weekly availability" description="Define recurring local business hours by barber.">
          <WeeklyAvailabilityEditor barbers={data.barbers} initialRules={data.availabilityRules} />
        </Section>

        <div className={styles.secondaryGrid}>
          <Section secondary title="Date exceptions" description="Close a day or replace normal hours for a specific date.">
            <ExceptionManager barbers={data.barbers} exceptions={data.availabilityExceptions} />
          </Section>
        </div>
      </div>
    </AppShell>
  );
}
