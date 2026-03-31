type StatusPillProps = {
  value: string;
};

export function StatusPill({ value }: StatusPillProps) {
  const tone = value.toLowerCase();
  return <span className={`status-pill status-pill--${tone}`}>{value}</span>;
}
