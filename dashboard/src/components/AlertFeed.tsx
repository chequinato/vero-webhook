import { Bell } from 'lucide-react';
import type { AlertItem } from '../types';

interface AlertFeedProps {
  alerts: AlertItem[];
}

export function AlertFeed({ alerts }: AlertFeedProps) {
  if (alerts.length === 0) {
    return (
      <div className="empty-state">
        <Bell size={28} />
        <p>Nenhum evento ainda. Envie transações para ver o feed em tempo real.</p>
      </div>
    );
  }

  return (
    <div className="alert-feed">
      {alerts.map(alert => (
        <div key={alert.id} className="alert-item">
          <div className={`alert-icon ${alert.type}`}>
            {alert.type === 'danger' ? '🚨' : alert.type === 'warning' ? '⚠️' : '✅'}
          </div>
          <div style={{ flex: 1 }}>
            <div className="alert-text">
              <strong>{alert.transacaoId}</strong> — {alert.message}
            </div>
            <div className="alert-time">
              {alert.timestamp.toLocaleTimeString('pt-BR')}
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
