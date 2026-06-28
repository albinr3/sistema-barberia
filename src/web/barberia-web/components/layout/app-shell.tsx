import Link from "next/link";
import Image from "next/image";
import type { Route } from "next";
import { CalendarDays, ClipboardList, DollarSign, Scissors, Settings, UserRound, Tv, Wrench, History, Menu, X } from "lucide-react";
import { LogoutButton } from "./logout-button";
import styles from "./app-shell.module.css";
import logo from "../../logo(2).png";

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
    { href: "/tickets-dashboard" as Route, label: "Tickets Dashboard", icon: Tv },
    { href: "/admin/tickets" as Route, label: "Ticket Ops", icon: Wrench },
    { href: "/admin/ticket-history" as Route, label: "Ticket History", icon: History },
    { href: "/admin/payroll" as Route, label: "Payroll", icon: DollarSign },
    { href: "/admin/admin-dashboard" as Route, label: "Admin Dashboard", icon: Scissors },
    { href: "/admin/sync", label: "Sync", icon: Settings },
  ],
};

export function AppShell({ title, variant, children }: AppShellProps) {
  const homeHref: Route = variant === "admin" ? "/admin" : variant === "barber" ? "/barber" : "/app";

  return (
    <div className={`${styles.shell} ${styles[variant]}`}>
      <input type="checkbox" id="mobile-menu-toggle" className={styles.menuToggle} aria-hidden="true" />
      <label htmlFor="mobile-menu-toggle" className={styles.backdrop} aria-hidden="true" />
      
      <header className={styles.mobileHeader}>
        <label htmlFor="mobile-menu-toggle" className={styles.hamburger} aria-label="Open menu">
          <Menu size={24} />
        </label>
        <Link href={homeHref} style={{ display: 'flex', alignItems: 'center' }}>
          <Image src={logo} alt="Logo" style={{ height: "36px", width: "auto" }} priority />
        </Link>
        <div style={{ width: 40 }}></div>
      </header>

      <aside className={styles.sidebar}>
        <div className={styles.sidebarHeader}>
          <label htmlFor="mobile-menu-toggle" className={styles.sidebarClose} aria-label="Close menu">
            <X size={24} />
          </label>
        </div>
        <Link className={styles.brand} href={homeHref} style={{ display: 'flex', alignItems: 'center', gap: '12px', textDecoration: 'none' }}>
          <Image src={logo} alt="Logo" style={{ height: "64px", width: "auto" }} priority />
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
