import { RiskScore } from './RiskScore';
import type { TransacaoDto } from '../types';

interface TransactionTableProps {
  transactions: TransacaoDto[];
  total: number;
  page: number;
  totalPages: number;
  filter: string;
  onFilterChange: (f: string) => void;
  onPageChange: (p: number) => void;
}

const STATUS_MAP: Record<string, { label: string; className: string }> = {
  aprovada: { label: 'APR', className: 'status--aprovada' },
  bloqueada: { label: 'BLK', className: 'status--bloqueada' },
  suspeita: { label: 'SUS', className: 'status--suspeita' },
  aceitaprovisoria: { label: 'PND', className: 'status--pendente' },
};

function StatusIndicator({ status }: { status: string }) {
  const normalized = status.replace(/_/g, '').toLowerCase();
  const mapped = STATUS_MAP[normalized] || { label: status.slice(0, 3).toUpperCase(), className: '' };
  return (
    <span className={`status-indicator ${mapped.className}`}>
      <span className="status-dot" />
      {mapped.label}
    </span>
  );
}

export function TransactionTable({
  transactions, total, page, totalPages,
  filter, onFilterChange, onPageChange,
}: TransactionTableProps) {
  const filters = [
    { key: '', label: 'All' },
    { key: 'bloqueada', label: 'Blocked' },
    { key: 'suspeita', label: 'Suspect' },
    { key: 'aprovada', label: 'Approved' },
    { key: 'aceitaprovisoria', label: 'Pending' },
  ];

  return (
    <div className="table-container">
      <div className="table-toolbar">
        <div className="table-title">
          Registros
          <span className="table-title-count">{total}</span>
        </div>
        <div className="table-filters">
          {filters.map(f => (
            <button
              key={f.key}
              className={`filter-btn ${filter === f.key ? 'active' : ''}`}
              onClick={() => onFilterChange(f.key)}
            >
              {f.label}
            </button>
          ))}
        </div>
      </div>

      {transactions.length === 0 ? (
        <div className="empty-state">
          <span>Nenhum registro encontrado</span>
        </div>
      ) : (
        <>
          <div className="data-table-wrap">
            <table>
              <thead>
                <tr>
                  <th>ID</th>
                  <th>Status</th>
                  <th>Valor</th>
                  <th>Tipo</th>
                  <th>Risk</th>
                  <th>Motivo</th>
                  <th>Timestamp</th>
                </tr>
              </thead>
              <tbody>
                {transactions.map((tx, i) => (
                  <tr key={tx.id} style={{ animationDelay: `${i * 30}ms` }}>
                    <td className="id-cell">{tx.id}</td>
                    <td><StatusIndicator status={tx.status} /></td>
                    <td className="value-cell">
                      R$ {tx.valor.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                    </td>
                    <td style={{ fontSize: 11, letterSpacing: '0.08em', textTransform: 'uppercase', color: 'var(--text-secondary)' }}>
                      {tx.tipo}
                    </td>
                    <td><RiskScore score={tx.riskScore} /></td>
                    <td className="motivo-cell">{tx.motivo || '—'}</td>
                    <td className="date-cell">
                      {new Date(tx.timestamp).toLocaleString('pt-BR', {
                        day: '2-digit',
                        month: '2-digit',
                        hour: '2-digit',
                        minute: '2-digit',
                      })}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="pagination">
            <span className="pagination-info">
              {page} / {totalPages}
            </span>
            <div className="pagination-controls">
              <button
                className="pagination-btn"
                disabled={page <= 1}
                onClick={() => onPageChange(page - 1)}
              >
                ← Prev
              </button>
              <button
                className="pagination-btn"
                disabled={page >= totalPages}
                onClick={() => onPageChange(page + 1)}
              >
                Next →
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
