interface RiskScoreProps {
  score: number | null;
}

export function RiskScore({ score }: RiskScoreProps) {
  if (score === null || score === undefined) {
    return <span style={{ color: 'var(--text-secondary)', fontFamily: 'var(--font-mono)', fontSize: 12 }}>—</span>;
  }

  const pct = score * 100;
  const level = pct > 70 ? 'high' : pct > 40 ? 'medium' : 'low';

  return (
    <span className="risk-display">
      <span className="risk-bar-track">
        <span
          className={`risk-bar-fill risk-bar-fill--${level}`}
          style={{ width: `${pct}%` }}
        />
      </span>
      <span className={`risk-value risk-value--${level}`}>
        {pct.toFixed(0)}%
      </span>
    </span>
  );
}
