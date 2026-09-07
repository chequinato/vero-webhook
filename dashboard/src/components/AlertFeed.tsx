import type { AlertItem } from '../types';

interface AlertFeedProps {
  alerts: AlertItem[];
}

const TAG_MAP: Record<string, { label: string; className: string }> = {
  danger: { label: 'BLOCK', className: 'feed-tag--danger' },
  warning: { label: 'ALERT', className: 'feed-tag--warning' },
  success: { label: 'PASS', className: 'feed-tag--success' },
};

export function AlertFeed({ alerts }: AlertFeedProps) {
  if (alerts.length === 0) {
    return (
      <div className="feed-empty">
        <div className="feed-empty-pulse" />
        <div>Aguardando eventos</div>
        <div style={{ marginTop: 4, opacity: 0.6 }}>
          Envie transações para ver o feed
        </div>
      </div>
    );
  }

  return (
    <div className="feed-container">
      {alerts.map((alert, i) => {
        const tag = TAG_MAP[alert.type] || TAG_MAP.success;
        const time = alert.timestamp.toLocaleTimeString('pt-BR', {
          hour: '2-digit',
          minute: '2-digit',
          second: '2-digit',
        });

        return (
          <div
            key={alert.id}
            className="feed-item"
            style={{ animationDelay: `${i * 40}ms` }}
          >
            <div className="feed-time">{time}</div>
            <div className={`feed-tag ${tag.className}`}>
              <span className="feed-tag-dot" />
              {tag.label}
            </div>
            <div className="feed-message">
              <strong>{alert.transacaoId}</strong>
              {' — '}
              {alert.motivo}
            </div>
          </div>
        );
      })}
    </div>
  );
}
