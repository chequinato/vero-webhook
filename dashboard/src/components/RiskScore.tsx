interface RiskScoreProps {
  score: number | null;
}

export function RiskScore({ score }: RiskScoreProps) {
  if (score === null || score === undefined) {
    return <span style={{ color: 'var(--text-muted)' }}>—</span>;
  }

  const pct = score * 100;
  const level = pct > 70 ? 'high' : pct > 40 ? 'medium' : 'low';
  const color = level === 'high' ? 'var(--danger)'
    : level === 'medium' ? 'var(--warning)' : 'var(--success)';

  return (
    <span style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
      <span className="risk-bar">
        <span
          className={`risk-bar-fill risk-${level}`}
          style={{ width: `${pct}%` }}
        />
      </span>
      <span style={{ color, fontSize: 12, fontWeight: 600 }}>
        {pct.toFixed(0)}%
      </span>
    </span>
  );
}
