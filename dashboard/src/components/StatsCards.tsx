import { useCountUp } from '../hooks/useCountUp';
import type { Stats } from '../types';

interface StatsCardsProps {
  stats: Stats | null;
  loading: boolean;
}

function AnimatedValue({ value, prefix = '', suffix = '', decimals = 0 }: {
  value: number;
  prefix?: string;
  suffix?: string;
  decimals?: number;
}) {
  const animated = useCountUp(value, 1200);
  const formatted = decimals > 0
    ? animated.toLocaleString('pt-BR', { minimumFractionDigits: decimals, maximumFractionDigits: decimals })
    : Math.round(animated).toLocaleString('pt-BR');
  return <>{prefix}{formatted}{suffix}</>;
}

export function StatsCards({ stats, loading }: StatsCardsProps) {
  if (loading || !stats) {
    return (
      <div className="overview-grid">
        {[...Array(7)].map((_, i) => (
          <div key={i} className={`overview-cell ${i === 0 ? 'overview-cell--hero' : ''}`}>
            <div className="overview-label loading-skeleton" style={{ width: 64, height: 10 }}>&nbsp;</div>
            <div className="overview-value loading-skeleton" style={{ width: i === 0 ? 160 : 80, height: i === 0 ? 56 : 28, marginTop: 8 }}>&nbsp;</div>
          </div>
        ))}
      </div>
    );
  }

  const pctAprovadas = stats.total > 0 ? ((stats.aprovadas / stats.total) * 100).toFixed(1) : '0';
  const pctBloqueadas = stats.total > 0 ? ((stats.bloqueadas / stats.total) * 100).toFixed(1) : '0';
  const pctSuspeitas = stats.total > 0 ? ((stats.suspeitas / stats.total) * 100).toFixed(1) : '0';

  return (
    <div className="overview-grid">
      {/* Hero: Total */}
      <div className="overview-cell overview-cell--hero stagger-1">
        <div className="overview-label">Transações</div>
        <div className="overview-value overview-value--hero">
          <AnimatedValue value={stats.total} />
        </div>
        <div className="overview-accent-line" />
        <div className="overview-sub">
          R$ {stats.valorTotal.toLocaleString('pt-BR', { minimumFractionDigits: 0, maximumFractionDigits: 0 })} volume total
        </div>
      </div>

      {/* Aprovadas */}
      <div className="overview-cell stagger-2">
        <div className="overview-label">Aprovadas</div>
        <div className="overview-value">
          <AnimatedValue value={stats.aprovadas} />
        </div>
        <div className="overview-pct overview-pct--success">{pctAprovadas}%</div>
      </div>

      {/* Bloqueadas */}
      <div className="overview-cell stagger-3">
        <div className="overview-label">Bloqueadas</div>
        <div className="overview-value">
          <AnimatedValue value={stats.bloqueadas} />
        </div>
        <div className="overview-pct overview-pct--danger">{pctBloqueadas}%</div>
      </div>

      {/* Suspeitas */}
      <div className="overview-cell stagger-4">
        <div className="overview-label">Suspeitas</div>
        <div className="overview-value">
          <AnimatedValue value={stats.suspeitas} />
        </div>
        <div className="overview-pct overview-pct--warning">{pctSuspeitas}%</div>
      </div>

      {/* Valor Médio */}
      <div className="overview-cell stagger-5">
        <div className="overview-label">Valor Médio</div>
        <div className="overview-value" style={{ fontSize: 24 }}>
          <AnimatedValue value={stats.valorMedio} prefix="R$ " decimals={2} />
        </div>
        <div className="overview-sub">por transação</div>
      </div>

      {/* Risk Score ML */}
      <div className="overview-cell stagger-6">
        <div className="overview-label">Risk Score ML</div>
        <div className="overview-value" style={{ fontSize: 24 }}>
          <AnimatedValue value={stats.riskScoreMedio * 100} suffix="%" decimals={1} />
        </div>
        <div className="overview-sub">média do modelo</div>
      </div>

      {/* Pendentes */}
      <div className="overview-cell stagger-7">
        <div className="overview-label">Pendentes</div>
        <div className="overview-value" style={{ fontSize: 24 }}>
          <AnimatedValue value={stats.aceitasProvisoria} />
        </div>
        <div className="overview-pct overview-pct--info">em análise</div>
      </div>
    </div>
  );
}
