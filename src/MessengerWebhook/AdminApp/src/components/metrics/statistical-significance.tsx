interface StatisticalSignificanceProps {
  pValue: number;
  isSignificant: boolean;
}

export function StatisticalSignificance({ pValue, isSignificant }: StatisticalSignificanceProps) {
  const getSignificanceLevel = () => {
    if (pValue < 0.01) return { label: 'Rất có ý nghĩa', color: 'green' };
    if (pValue < 0.05) return { label: 'Có ý nghĩa', color: 'blue' };
    return { label: 'Không có ý nghĩa', color: 'gray' };
  };

  const { label, color } = getSignificanceLevel();

  return (
    <div className={`significance-badge significance-${color}`}>
      <span className="badge-label">{label}</span>
      <span className="badge-value">p = {pValue.toFixed(4)}</span>
      {isSignificant && <span className="badge-icon">✓</span>}
    </div>
  );
}
