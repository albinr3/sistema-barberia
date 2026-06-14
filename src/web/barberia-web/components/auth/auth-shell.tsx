import styles from "./auth-shell.module.css";

export function AuthShell({ children }: { children: React.ReactNode }) {
  return (
    <main className={styles.page}>
      <section className={styles.brandPanel} aria-label="Book your appointment at our barbershop">
        <div className={styles.brandCopy}>
          <p className={styles.kicker}>Welcome to our Barbershop</p>
          <h1>Your style, at your own pace. Book your appointment in seconds.</h1>
          <p className={styles.copy}>
            Explore our services, choose your favorite barber, and book your spot quickly and easily.
          </p>
        </div>
        <div className={styles.signalGrid} aria-hidden="true">
          <span>Haircuts</span>
          <span>Beards</span>
          <span>Bookings</span>
          <span>Style</span>
        </div>
      </section>
      <section className={styles.formPanel}>{children}</section>
    </main>
  );
}
