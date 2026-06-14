import { StatusBadge } from "@/components/ui/status-badge";
import styles from "./placeholder-panel.module.css";

type PlaceholderPanelProps = {
  title: string;
  status?: string;
  children: React.ReactNode;
};

export function PlaceholderPanel({ title, status = "Phase 2.0", children }: PlaceholderPanelProps) {
  return (
    <section className={styles.panel}>
      <div className={styles.header}>
        <h2>{title}</h2>
        <StatusBadge tone="warning">{status}</StatusBadge>
      </div>
      <p>{children}</p>
    </section>
  );
}
