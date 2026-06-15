import Link from "next/link";
import type { Route } from "next";
import { CalendarDays, ClipboardList, Scissors, Settings, UserRound, Tv } from "lucide-react";
import { LogoutButton } from "./logout-button";
import styles from "./app-shell.module.css";

type AppShellProps = {
  title: string;
  variant: "customer" | "barber" | "admin";
  children: React.ReactNode;
};

type NavItem = {
  href: Route;
  label: string;
  icon: typeof CalendarDays;
};

const navItems: Record<AppShellProps["variant"], NavItem[]> = {
  customer: [
    { href: "/app/book", label: "Book", icon: CalendarDays },
    { href: "/app/appointments", label: "Appointments", icon: ClipboardList },
    { href: "/app/profile", label: "Profile", icon: UserRound },
  ],
  barber: [
    { href: "/barber", label: "Schedule", icon: Scissors },
    { href: "/barber/settings", label: "Settings", icon: Settings },
  ],
  admin: [
    { href: "/admin", label: "Dashboard", icon: ClipboardList },
    { href: "/admin/appointments", label: "Appointments", icon: CalendarDays },
    { href: "/admin/admin-dashboard" as Route, label: "Admin Dashboard", icon: Scissors },
    { href: "/admin/sync", label: "Sync", icon: Settings },
    { href: "/tickets-dashboard" as Route, label: "Tickets Dashboard", icon: Tv },
  ],
};

export function AppShell({ title, variant, children }: AppShellProps) {
  const homeHref: Route = variant === "admin" ? "/admin" : variant === "barber" ? "/barber" : "/app";

  return (
    <div className={`${styles.shell} ${styles[variant]}`}>
      <aside className={styles.sidebar}>
        <Link className={styles.brand} href={homeHref}>
          <span>System</span>
          Barbershop
        </Link>
        <nav aria-label={`${variant} navigation`}>
          {navItems[variant].map((item) => {
            const Icon = item.icon;
            return (
              <Link key={item.href} href={item.href}>
                <Icon size={18} aria-hidden="true" />
                {item.label}
              </Link>
            );
          })}
        </nav>
      </aside>
      <main className={styles.content}>
        <header className={styles.header}>
          <h1>{title}</h1>
          <LogoutButton />
        </header>
        {children}
      </main>
    </div>
  );
}
