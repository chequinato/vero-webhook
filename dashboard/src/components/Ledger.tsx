import { Risk } from './Risk';
import type { TransacaoDto } from '../types';

/**
 * 04 — REGISTRO
 *
 * Tabela sem moldura: só fios horizontais, como um livro-caixa impresso.
 * O estado é um glifo geométrico (cheio / meio / vazio / cortado) antes de
 * ser uma cor, para continuar legível em impressão e em daltonismo.
 */

interface Props {
  transactions: TransacaoDto[];
  total: number;
  page: number;
  totalPages: number;
  filter: string;
  onFilterChange: (f: string) => void;
  onPageChange: (p: number) => void;
}

const STATE: Record<string, { code: string; cls: string }> = {
  aprovada: { code: 'apr', cls: 'aprovada' },
  bloqueada: { code: 'blq', cls: 'bloqueada' },
  suspeita: { code: 'sus', cls: 'suspeita' },
  aceitaprovisoria: { code: 'pnd', cls: 'pendente' },
};

const FILTERS = [
  { key: '', label: 'Tudo' },
  { key: 'bloqueada', label: 'Bloqueadas' },
  { key: 'suspeita', label: 'Suspeitas' },
  { key: 'aprovada', label: 'Aprovadas' },
  { key: 'aceitaprovisoria', label: 'Em análise' },
];

function State({ status }: { status: string }) {
  const key = status.replace(/_/g, '').toLowerCase();
  const s = STATE[key] ?? { code: status.slice(0, 3).toLowerCase(), cls: '' };
  return (
    <span className={`state state--${s.cls}`}>
      <i />
      {s.code}
    </span>
  );
}

export function Ledger({ transactions, total, page, totalPages, filter, onFilterChange, onPageChange }: Props) {
  return (
    <section>
      <div className="log-head">
        <h2 className="log-title rv-cut">
          Registro
          <span>{total.toLocaleString('pt-BR')} linhas</span>
        </h2>
        <div className="filters">
          {FILTERS.map(f => (
            <button
              key={f.key}
              className={`filter${filter === f.key ? ' on' : ''}`}
              onClick={() => onFilterChange(f.key)}
            >
              {f.label}
            </button>
          ))}
        </div>
      </div>

      {transactions.length === 0 ? (
        <div className="empty">Nenhuma linha sob este filtro</div>
      ) : (
        <>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th style={{ width: '20%' }}>Identificador</th>
                  <th style={{ width: '8%' }}>Estado</th>
                  <th style={{ width: '14%' }}>Valor</th>
                  <th style={{ width: '10%' }}>Tipo</th>
                  <th style={{ width: '16%' }}>Risco</th>
                  <th style={{ width: '20%' }}>Motivo</th>
                  <th style={{ width: '12%' }}>Carimbo</th>
                </tr>
              </thead>
              <tbody>
                {transactions.map((tx, i) => (
                  <tr key={tx.id} style={{ animationDelay: `${Math.min(i, 12) * 22}ms` }}>
                    <td className="cell-id">{tx.id}</td>
                    <td>
                      <State status={tx.status} />
                    </td>
                    <td className="cell-value">
                      {tx.moeda === 'BRL' || !tx.moeda ? 'R$' : tx.moeda}{' '}
                      {tx.valor.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                    </td>
                    <td className="cell-type">{tx.tipo}</td>
                    <td>
                      <Risk score={tx.riskScore} />
                    </td>
                    <td className="cell-reason">{tx.motivo || '——'}</td>
                    <td className="cell-date">
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

          <div className="pager">
            <span className="pager-info">
              Página <b>{page}</b> de <b>{totalPages}</b>
            </span>
            <div className="pager-btns">
              <button className="pager-btn" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
                ← Anterior
              </button>
              <button className="pager-btn" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>
                Próxima →
              </button>
            </div>
          </div>
        </>
      )}
    </section>
  );
}
