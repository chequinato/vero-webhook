import { Shield, ShieldAlert, ShieldCheck, Activity, DollarSign, Brain } from 'lucide-react';
import type { Stats } from '../types';

interface StatsCardsProps {
  stats: Stats | null;
  loading: boolean;
}

export function StatsCards({ stats, loading }: StatsCardsProps) {
  if (loading || !stats) {
    return (
      <div className="stats-grid">
        {[...Array(6)].map((_, i) => (
          <div key={i} className="stat-card loading">
            <div className="stat-card-header">
              <span className="stat-card-label">Carregando...</span>
            </div>
            <div className="stat-card-value">—</div>
          </div>
        ))}
      </div>
    );
  }

  const cards = [
    {
      label: 'Total',
      value: stats.total.toLocaleString('pt-BR'),
      icon: <Activity size={18} />,
      sub: `R$ ${stats.valorTotal.toLocaleString('pt-BR', { minimumFractionDigits: 2 })} total`,
      color: 'var(--accent)',
    },
    {
      label: 'Aprovadas',
      value: stats.aprovadas.toLocaleString('pt-BR'),
      icon: <ShieldCheck size={18} />,
      sub: stats.total > 0
        ? `${((stats.aprovadas / stats.total) * 100).toFixed(1)}% do total`
        : '—',
      color: 'var(--success)',
    },
    {
      label: 'Bloqueadas',
      value: stats.bloqueadas.toLocaleString('pt-BR'),
      icon: <ShieldAlert size={18} />,
      sub: stats.total > 0
        ? `${((stats.bloqueadas / stats.total) * 100).toFixed(1)}% do total`
        : '—',
      color: 'var(--danger)',
    },
    {
      label: 'Suspeitas',
      value: stats.suspeitas.toLocaleString('pt-BR'),
      icon: <Shield size={18} />,
      sub: stats.total > 0
        ? `${((stats.suspeitas / stats.total) * 100).toFixed(1)}% do total`
        : '—',
      color: 'var(--warning)',
    },
    {
      label: 'Valor Médio',
      value: `R$ ${stats.valorMedio.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`,
      icon: <DollarSign size={18} />,
      sub: 'por transação',
      color: 'var(--info)',
    },
    {
      label: 'Risk Score ML',
      value: `${(stats.riskScoreMedio * 100).toFixed(1)}%`,
      icon: <Brain size={18} />,
      sub: 'média do modelo',
      color: 'var(--purple)',
    },
  ];

  return (
    <div className="stats-grid">
      {cards.map((card) => (
        <div key={card.label} className="stat-card">
          <div className="stat-card-header">
            <span className="stat-card-label">{card.label}</span>
            <span className="stat-card-icon" style={{ color: card.color }}>
              {card.icon}
            </span>
          </div>
          <div className="stat-card-value" style={{ color: card.color }}>
            {card.value}
          </div>
          <div className="stat-card-sub">{card.sub}</div>
        </div>
      ))}
    </div>
  );
}
