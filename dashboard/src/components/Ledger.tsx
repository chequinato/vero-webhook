import { useState } from 'react';
import { Risk } from './Risk';
import { chaveStatus } from '../live';
import type { TransacaoDto, Reavaliacao } from '../types';

/**
 * 06 — REGISTRO
 *
 * Tabela sem moldura: só fios horizontais, como um livro-caixa impresso.
 * O estado é um glifo geométrico (cheio / meio / vazio / cortado) antes de
 * ser uma cor, para continuar legível em impressão e em daltonismo.
 *
 * A última coluna é a fila de trabalho: toda linha ainda em revisão ganha um
 * punção de avaliação. Apertar não decide nada — manda o modelo decidir, e a
 * linha se resolve em aprovada ou bloqueada na frente do operador.
 */

interface Props {
  transactions: TransacaoDto[];
  total: number;
  page: number;
  totalPages: number;
  filter: string;
  onFilterChange: (f: string) => void;
  onPageChange: (p: number) => void;
  onReavaliar: (id: string) => Promise<Reavaliacao>;
}

const STATE: Record<string, { code: string; cls: string }> = {
  aprovada: { code: 'apr', cls: 'aprovada' },
  bloqueada: { code: 'blq', cls: 'bloqueada' },
  suspeita: { code: 'sus', cls: 'suspeita' },
  aceitaprovisoria: { code: 'pnd', cls: 'pendente' },
};

/** Estados que ainda esperam veredito e portanto podem ser reavaliados. */
const PENDENTES = new Set(['suspeita', 'aceitaprovisoria']);

const FILTERS = [
  { key: '', label: 'Tudo' },
  { key: 'bloqueada', label: 'Bloqueadas' },
  { key: 'suspeita', label: 'Suspeitas' },
  { key: 'aprovada', label: 'Aprovadas' },
  { key: 'aceitaprovisoria', label: 'Em análise' },
];

function State({ status }: { status: string }) {
  const key = chaveStatus(status);
  const s = STATE[key] ?? { code: status.slice(0, 3).toLowerCase(), cls: '' };
  return (
    <span className={`state state--${s.cls}`}>
      <i />
      {s.code}
    </span>
  );
}

type Fase = 'parado' | 'rodando' | 'erro';

export function Ledger({
  transactions,
  total,
  page,
  totalPages,
  filter,
  onFilterChange,
  onPageChange,
  onReavaliar,
}: Props) {
  const [fases, setFases] = useState<Record<string, Fase>>({});
  const [erros, setErros] = useState<Record<string, string>>({});
  const [resolvidas, setResolvidas] = useState<Record<string, string>>({});

  const pendentesNaPagina = transactions.filter(t => PENDENTES.has(chaveStatus(t.status))).length;

  const avaliar = async (id: string) => {
    setFases(f => ({ ...f, [id]: 'rodando' }));
    setErros(e => {
      if (!(id in e)) return e;
      const resto = { ...e };
      delete resto[id];
      return resto;
    });

    try {
      const r = await onReavaliar(id);
      setFases(f => ({ ...f, [id]: 'parado' }));
      setResolvidas(v => ({ ...v, [id]: r.status }));
    } catch (err) {
      setFases(f => ({ ...f, [id]: 'erro' }));
      setErros(e => ({ ...e, [id]: err instanceof Error ? err.message : 'falha na avaliação' }));
    }
  };

  return (
    <section>
      <div className="log-head">
        <h2 className="log-title rv-cut">
          <span className="idx log-idx">06</span>
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
                  <th style={{ width: '19%' }}>Identificador</th>
                  <th style={{ width: '7%' }}>Estado</th>
                  <th style={{ width: '13%' }}>Valor</th>
                  <th style={{ width: '8%' }}>Tipo</th>
                  <th style={{ width: '15%' }}>Risco</th>
                  <th style={{ width: '18%' }}>Motivo</th>
                  <th style={{ width: '11%' }}>Carimbo</th>
                  <th style={{ width: '9%' }} className="th-acao">
                    Análise
                    {pendentesNaPagina > 0 && <b>{pendentesNaPagina} na fila</b>}
                  </th>
                </tr>
              </thead>
              <tbody>
                {transactions.map((tx, i) => {
                  const pendente = PENDENTES.has(chaveStatus(tx.status));
                  const fase = fases[tx.id] ?? 'parado';
                  const veredito = resolvidas[tx.id];

                  return (
                    <tr
                      key={tx.id}
                      className={veredito ? `resolvida resolvida--${veredito}` : undefined}
                      style={{ animationDelay: `${Math.min(i, 12) * 22}ms` }}
                    >
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
                      <td className="cell-acao">
                        {pendente ? (
                          <button
                            className={`puncao${fase === 'rodando' ? ' puncao--rodando' : ''}${
                              fase === 'erro' ? ' puncao--erro' : ''
                            }`}
                            onClick={() => avaliar(tx.id)}
                            disabled={fase === 'rodando'}
                            title={
                              erros[tx.id] ??
                              'Submeter ao modelo — a decisão é dele, não sua'
                            }
                          >
                            <span className="puncao-txt">
                              {fase === 'rodando' ? 'avaliando' : fase === 'erro' ? 'repetir' : 'avaliar'}
                            </span>
                            <span className="puncao-fio" />
                          </button>
                        ) : veredito ? (
                          <span className={`veredito veredito--${veredito}`}>
                            {veredito === 'bloqueada' ? 'bloqueada pelo modelo' : 'liberada pelo modelo'}
                          </span>
                        ) : (
                          <span className="cell-vazio">——</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
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
