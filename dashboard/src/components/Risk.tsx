/**
 * Medidor de risco: dez traços, como a escala gravada de um instrumento.
 * A leitura é posicional antes de ser numérica — dá para varrer a coluna
 * inteira com o olho sem ler um único número.
 */
export function Risk({ score }: { score: number | null }) {
  if (score === null || score === undefined) {
    return <span style={{ color: 'var(--ink-25)' }}>——</span>;
  }

  const pct = Math.max(0, Math.min(1, score)) * 100;
  const on = Math.round(pct / 10);
  const level = pct > 70 ? 'high' : pct > 40 ? 'med' : 'low';

  return (
    <span className="risk">
      <span className="risk-ticks">
        {Array.from({ length: 10 }, (_, i) => (
          <span
            key={i}
            className={`risk-tick${i < on ? ` risk-tick--${level}` : ''}`}
            style={{ height: 9 + i * 1.1, transitionDelay: `${i * 16}ms` }}
          />
        ))}
      </span>
      <span className={`risk-val risk-val--${level}`}>{pct.toFixed(0)}%</span>
    </span>
  );
}
