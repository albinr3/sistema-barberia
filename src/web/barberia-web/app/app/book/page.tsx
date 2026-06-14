import { AppShell } from "@/components/layout/app-shell";
import { requireCustomer } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";
import { getActiveServices, getBookingBarbers } from "@/lib/booking/queries";
import { BookingStepper } from "@/components/booking/booking-stepper";
import styles from "./BookPage.module.css";

export default async function BookPage() {
  const supabase = await createClient();
  await requireCustomer(supabase);

  const [services, barbers] = await Promise.all([
    getActiveServices(supabase),
    getBookingBarbers(supabase),
  ]);

  return (
    <AppShell title="Book appointment" variant="customer">
      <div className={styles.pageContainer}>
        <BookingStepper
          services={services}
          barbers={barbers}
        />
      </div>
    </AppShell>
  );
}
