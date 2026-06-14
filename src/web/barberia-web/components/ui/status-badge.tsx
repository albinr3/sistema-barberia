import styles from "./status-badge.module.css";

type StatusBadgeProps = {
  tone?: "neutral" | "success" | "warning" | "danger";
  children: React.ReactNode;
};

export function StatusBadge({ tone = "neutral", children }: StatusBadgeProps) {
  return <span className={`${styles.badge} ${styles[tone]}`}>{children}</span>;
}
