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

const STATUS_LABELS: Record<string, string> = {
  aprovada: 'Aprovada',
  bloqueada: 'Bloqueada',
  suspeita: 'Suspeita',
  aceitaprovisoria: 'Pendente',
};

function StatusBadge({ status }: { status: string }) {
  const normalized = status.replace(/_/g, '').toLowerCase();
  return (
    <span className={`badge ${normalized}`}>
      {STATUS_LABELS[normalized] || status}
    </span>
  );
}

export function TransactionTable({
  transactions, total, page, totalPages,
  filter, onFilterChange, onPageChange,
}: TransactionTableProps) {
  const filters = [
    { key: '', label: 'Todas' },
    { key: 'bloqueada', label: 'Bloqueadas' },
    { key: 'suspeita', label: 'Suspeitas' },
    { key: 'aprovada', label: 'Aprovadas' },
    { key: 'aceitaprovisoria', label: 'Pendentes' },
  ];

  return (
    <div className="table-card">
      <div className="table-header">
        <h3>Transações ({total})</h3>
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
          <p>Nenhuma transação encontrada</p>
        </div>
      ) : (
        <>
          <div style={{ overflowX: 'auto' }}>
            <table>
              <thead>
                <tr>
                  <th>ID</th>
                  <th>Status</th>
                  <th>Valor</th>
                  <th>Tipo</th>
                  <th>Risk Score</th>
                  <th>Motivo</th>
                  <th>Data</th>
                </tr>
              </thead>
              <tbody>
                {transactions.map(tx => (
                  <tr key={tx.id}>
                    <td style={{ fontFamily: 'monospace', fontSize: 12 }}>{tx.id}</td>
                    <td><StatusBadge status={tx.status} /></td>
                    <td style={{ fontWeight: 600 }}>
                      R$ {tx.valor.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                    </td>
                    <td style={{ textTransform: 'uppercase', fontSize: 11, color: 'var(--text-muted)' }}>
                      {tx.tipo}
                    </td>
                    <td><RiskScore score={tx.riskScore} /></td>
                    <td style={{ color: 'var(--text-secondary)', fontSize: 12 }}>
                      {tx.motivo || '—'}
                    </td>
                    <td style={{ fontSize: 12, color: 'var(--text-muted)' }}>
                      {new Date(tx.timestamp).toLocaleString('pt-BR')}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="pagination">
            <span className="pagination-info">
              Página {page} de {totalPages}
            </span>
            <div className="pagination-btns">
              <button disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
                ← Anterior
              </button>
              <button disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>
                Próxima →
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
