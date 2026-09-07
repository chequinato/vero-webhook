import type { AlertItem } from '../types';

/**
 * 03 — FEED
 *
 * Fita de eventos em mono, dentro da zona invertida. Cada linha entra por
 * varredura lateral (sem fade) e dá um único lampejo por diferença de cor:
 * o olho registra a chegada e a linha volta a ser texto.
 */

const TAG: Record<AlertItem['type'], string> = {
  danger: 'BLQ',
  warning: 'SUS',
  success: 'OK',
};

export function Feed({ alerts }: { alerts: AlertItem[] }) {
  if (alerts.length === 0) {
    return (
      <div className="feed-empty">
        <i />
        Escutando o barramento
      </div>
    );
  }

  return (
    <div className="feed">
      {alerts.map((a, i) => (
        <div key={a.id} className="feed-row" style={{ animationDelay: `${Math.min(i, 8) * 26}ms` }}>
          <span className="feed-idx">{String(alerts.length - i).padStart(4, '0')}</span>
          <span className="feed-time">
            {a.timestamp.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
          </span>
          <span className={`feed-tag feed-tag--${a.type}`}>{TAG[a.type]}</span>
          <span className="feed-msg">
            <b>{shortId(a.transacaoId)}</b> {a.motivo}
          </span>
        </div>
      ))}
    </div>
  );
}

function shortId(id: string): string {
  return id.length > 10 ? `${id.slice(0, 8)}…` : id;
}
