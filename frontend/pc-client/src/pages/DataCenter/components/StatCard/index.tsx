import styles from './StatCard.module.css';

interface StatCardProps {
  label: string;
  value: string | number;
  subLabel?: string;
}

export function StatCard({ label, value, subLabel }: StatCardProps) {
  return (
    <div className={styles.card}>
      <span className={styles.label}>{label}</span>
      <span className={styles.value}>{value}</span>
      {subLabel && <span className={styles.subLabel}>{subLabel}</span>}
    </div>
  );
}