import { Fragment, useMemo, useState } from 'react';
import { Risk } from './Risk';
import { chaveStatus } from '../live';
import type { TransacaoDto, Reavaliacao } from '../types';

/**
 * 07 — REGISTRO
 *
 * Tabela sem moldura: só fios horizontais, como um livro-caixa impresso.
 * O estado é um glifo geométrico (cheio / meio / vazio / cortado) antes de
 * ser uma cor, para continuar legível em impressão e em daltonismo.
 *
 * Três gestos de tabela de log vieram de fora e foram redesenhados aqui:
 *
 *  · **Ordenação por cabeçalho.** O sinal de direção é um caractere mono
 *    (▲ ▼), não um ícone. E o rótulo diz em voz alta que a ordenação vale
 *    só para a página carregada — a paginação é do servidor, e uma tabela
 *    que finge ordenar o livro inteiro está mentindo.
 *
 *  · **Dossiê expansível.** Clicar na linha abre uma gaveta com o que não
 *    coube nas colunas. Ela abre por corte de baixo para cima, na mesma
 *    gramática das células da matriz — não é um modal, é a própria linha
 *    crescendo.
 *
 *  · **Exportação da página.** Um botão de expediente, em texto, que
 *    entrega exatamente as linhas visíveis em CSV.
 *
 * A última coluna continua sendo a fila de trabalho: toda linha ainda em
 * revisão ganha um punção de avaliação. Apertar não decide nada — manda o
 * modelo decidir, e a linha se resolve na frente do operador.
 */

interface Props {
  transactions: TransacaoDto[];
  total: number;
  page: number;
  totalPages: number;
  filter: string;
  /** Corte de bloqueio do modelo, impresso na régua do dossiê. */
  limiar?: number;
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

type Campo = 'id' | 'status' | 'valor' | 'tipo' | 'riskScore' | 'timestamp';

const COLUNAS: { campo: Campo | null; rotulo: string; largura: string }[] = [
  { campo: 'id', rotulo: 'Identificador', largura: '19%' },
  { campo: 'status', rotulo: 'Estado', largura: '7%' },
  { campo: 'valor', rotulo: 'Valor', largura: '13%' },
  { campo: 'tipo', rotulo: 'Tipo', largura: '8%' },
  { campo: 'riskScore', rotulo: 'Risco', largura: '15%' },
  { campo: null, rotulo: 'Motivo', largura: '18%' },
  { campo: 'timestamp', rotulo: 'Carimbo', largura: '11%' },
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

function moedaDe(tx: TransacaoDto) {
  return tx.moeda === 'BRL' || !tx.moeda ? 'R$' : tx.moeda;
}

export function Ledger({
  transactions,
  total,
  page,
  totalPages,
  filter,
  limiar = 0.7,
  onFilterChange,
  onPageChange,
  onReavaliar,
}: Props) {
  const [fases, setFases] = useState<Record<string, Fase>>({});
  const [erros, setErros] = useState<Record<string, string>>({});
  const [resolvidas, setResolvidas] = useState<Record<string, string>>({});
  const [ordem, setOrdem] = useState<{ campo: Campo; dir: 1 | -1 } | null>(null);
  const [aberta, setAberta] = useState<string | null>(null);
  const [copiada, setCopiada] = useState<string | null>(null);

  const pendentesNaPagina = transactions.filter(t => PENDENTES.has(chaveStatus(t.status))).length;

  // Sem ordenação escolhida a lista fica exatamente como o servidor mandou —
  // é a ordem cronológica do livro, e ela é uma informação por si só.
  const linhas = useMemo(() => {
    if (!ordem) return transactions;
    const { campo, dir } = ordem;
    return [...transactions].sort((x, y) => {
      const a = campo === 'riskScore' ? (x.riskScore ?? -1) : x[campo];
      const b = campo === 'riskScore' ? (y.riskScore ?? -1) : y[campo];
      if (typeof a === 'number' && typeof b === 'number') return (a - b) * dir;
      return String(a).localeCompare(String(b), 'pt-BR') * dir;
    });
  }, [transactions, ordem]);

  // Três estados por coluna: ascendente, descendente, e de volta à ordem do
  // servidor. Sem o terceiro, não há como desfazer uma ordenação.
  const ordenarPor = (campo: Campo) =>
    setOrdem(o =>
      o?.campo === campo ? (o.dir === 1 ? { campo, dir: -1 } : null) : { campo, dir: 1 },
    );

  const copiar = async (id: string) => {
    try {
      await navigator.clipboard.writeText(id);
      setCopiada(id);
      window.setTimeout(() => setCopiada(c => (c === id ? null : c)), 1400);
    } catch {
      /* área de transferência negada — o identificador continua selecionável */
    }
  };

  /**
   * Exporta a página tal como está na tela, ordenação inclusive. Ponto e
   * vírgula como separador e BOM na frente: é o que faz o Excel em
   * português abrir o arquivo sem esparramar tudo numa coluna só.
   */
  const exportar = () => {
    const cab = ['id', 'status', 'valor', 'moeda', 'tipo', 'risco', 'motivo', 'carimbo'];
    const corpo = linhas.map(t =>
      [
        t.id,
        t.status,
        t.valor.toFixed(2).replace('.', ','),
        t.moeda || 'BRL',
        t.tipo,
        t.riskScore === null ? '' : t.riskScore.toFixed(3).replace('.', ','),
        (t.motivo ?? '').replace(/[;\n]/g, ' '),
        t.timestamp,
      ].join(';'),
    );

    const blob = new Blob(['﻿' + [cab.join(';'), ...corpo].join('\r\n')], {
      type: 'text/csv;charset=utf-8',
    });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `vero-registro-p${page}${filter ? `-${filter}` : ''}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

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
          <span className="idx log-idx">07</span>
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
          <span className="filters-fio" />
          <button
            className="filter filter--acao"
            onClick={exportar}
            disabled={linhas.length === 0}
            title="Baixa exatamente as linhas desta página, na ordem em que estão"
          >
            Exportar página
          </button>
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
                  {COLUNAS.map(c => (
                    <th key={c.rotulo} style={{ width: c.largura }}>
                      {c.campo ? (
                        <button
                          className={`th-sort${ordem?.campo === c.campo ? ' on' : ''}`}
                          onClick={() => ordenarPor(c.campo as Campo)}
                          title={`Ordenar por ${c.rotulo.toLowerCase()} — vale para esta página`}
                        >
                          {c.rotulo}
                          <i>{ordem?.campo === c.campo ? (ordem.dir === 1 ? '▲' : '▼') : '·'}</i>
                        </button>
                      ) : (
                        c.rotulo
                      )}
                    </th>
                  ))}
                  <th style={{ width: '9%' }} className="th-acao">
                    Análise
                    {pendentesNaPagina > 0 && <b>{pendentesNaPagina} na fila</b>}
                  </th>
                </tr>
              </thead>
              <tbody>
                {linhas.map((tx, i) => {
                  const pendente = PENDENTES.has(chaveStatus(tx.status));
                  const fase = fases[tx.id] ?? 'parado';
                  const veredito = resolvidas[tx.id];
                  const escancarada = aberta === tx.id;

                  // As duas linhas (a do livro e a gaveta) vao dentro de um
                  // Fragment com chave propria. Devolver um array cru daqui faz
                  // o React casar os filhos pela POSICAO do array externo: uma
                  // transacao nova entrando no topo desloca todo mundo, o casamento
                  // por chave se perde, e cada linha remonta — o que reiniciava a
                  // animacao da gaveta a cada evento e a deixava fechada para sempre.
                  return (
                    <Fragment key={tx.id}>
                    <tr
                      className={[
                        escancarada ? 'linha--aberta' : '',
                        veredito ? `resolvida resolvida--${veredito}` : '',
                      ]
                        .filter(Boolean)
                        .join(' ')}
                      style={{ animationDelay: `${Math.min(i, 12) * 22}ms` }}
                      onClick={() => setAberta(a => (a === tx.id ? null : tx.id))}
                    >
                      <td className="cell-id">
                        <span className="cell-caret">{escancarada ? '▾' : '▸'}</span>
                        {tx.id}
                      </td>
                      <td>
                        <State status={tx.status} />
                      </td>
                      <td className="cell-value">
                        {moedaDe(tx)}{' '}
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
                      <td className="cell-acao" onClick={e => e.stopPropagation()}>
                        {pendente ? (
                          <button
                            className={`puncao${fase === 'rodando' ? ' puncao--rodando' : ''}${
                              fase === 'erro' ? ' puncao--erro' : ''
                            }`}
                            onClick={() => avaliar(tx.id)}
                            disabled={fase === 'rodando'}
                            title={erros[tx.id] ?? 'Submeter ao modelo — a decisão é dele, não sua'}
                          >
                            <span className="puncao-txt">
                              {fase === 'rodando'
                                ? 'avaliando'
                                : fase === 'erro'
                                  ? 'repetir'
                                  : 'avaliar'}
                            </span>
                            <span className="puncao-fio" />
                          </button>
                        ) : veredito ? (
                          <span className={`veredito veredito--${veredito}`}>
                            {veredito === 'bloqueada'
                              ? 'bloqueada pelo modelo'
                              : 'liberada pelo modelo'}
                          </span>
                        ) : (
                          <span className="cell-vazio">——</span>
                        )}
                      </td>
                    </tr>

                    {escancarada && (
                      <tr className="dossie-linha">
                        <td colSpan={8}>
                          <Dossie
                            tx={tx}
                            limiar={limiar}
                            copiada={copiada === tx.id}
                            onCopiar={() => copiar(tx.id)}
                          />
                        </td>
                      </tr>
                    )}
                    </Fragment>
                  );
                })}
              </tbody>
            </table>
          </div>

          <div className="pager">
            <span className="pager-info">
              Página <b>{page}</b> de <b>{totalPages}</b>
              {ordem && <em className="pager-nota">ordenação local desta página</em>}
            </span>
            <div className="pager-btns">
              <button
                className="pager-btn"
                disabled={page <= 1}
                onClick={() => onPageChange(page - 1)}
              >
                ← Anterior
              </button>
              <button
                className="pager-btn"
                disabled={page >= totalPages}
                onClick={() => onPageChange(page + 1)}
              >
                Próxima →
              </button>
            </div>
          </div>
        </>
      )}
    </section>
  );
}

/**
 * A gaveta da linha. Só campos que existem de fato no registro — nada de
 * inventar histórico ou explicação de modelo que a API não devolve.
 */
function Dossie({
  tx,
  limiar,
  copiada,
  onCopiar,
}: {
  tx: TransacaoDto;
  limiar: number;
  copiada: boolean;
  onCopiar: () => void;
}) {
  const escore = tx.riskScore ?? 0;
  const evento = new Date(tx.timestamp).getTime();
  const registro = new Date(tx.createdAt).getTime();
  // Defasagem de ingestão: quanto o evento levou do carimbo de origem até
  // virar linha no livro. É o número que denuncia fila represada.
  const defasagem = Number.isFinite(evento) && Number.isFinite(registro) ? registro - evento : null;

  return (
    <div className="dossie">
      <div className="dossie-col">
        <span className="dossie-lbl">Identificador</span>
        <div className="dossie-id">
          <code>{tx.id}</code>
          <button className="dossie-copiar" onClick={onCopiar}>
            {copiada ? 'copiado' : 'copiar'}
          </button>
        </div>

        <span className="dossie-lbl">Contraparte</span>
        <div className="dossie-par">
          <b>{tx.remetenteId}</b>
          <span className="dossie-seta" />
          <b>{tx.destinatarioId}</b>
        </div>
      </div>

      <div className="dossie-col">
        <span className="dossie-lbl">Escore contra o corte do modelo</span>
        <div className="dossie-regua">
          <span className="dossie-regua-trilho">
            <i
              className={`dossie-regua-barra${escore >= limiar ? ' acima' : ''}`}
              style={{ width: `${Math.min(100, escore * 100)}%` }}
            />
            <i className="dossie-regua-corte" style={{ left: `${limiar * 100}%` }} />
          </span>
          <span className="dossie-regua-nums">
            <b>{(escore * 100).toFixed(1)}</b>
            <span>corte em {(limiar * 100).toFixed(0)}</span>
          </span>
        </div>

        <span className="dossie-lbl">Motivo registrado</span>
        <p className="dossie-motivo">{tx.motivo || 'Nenhum motivo anotado — passou limpo.'}</p>
      </div>

      <div className="dossie-col">
        <span className="dossie-lbl">Carimbo do evento</span>
        <b className="dossie-val">{new Date(tx.timestamp).toLocaleString('pt-BR')}</b>

        <span className="dossie-lbl">Entrada no livro</span>
        <b className="dossie-val">{new Date(tx.createdAt).toLocaleString('pt-BR')}</b>

        <span className="dossie-lbl">Defasagem de ingestão</span>
        <b className={`dossie-val${defasagem !== null && defasagem > 60000 ? ' alerta' : ''}`}>
          {defasagem === null
            ? '——'
            : defasagem < 1000
              ? '< 1 s'
              : defasagem < 60000
                ? `${(defasagem / 1000).toFixed(1)} s`
                : `${(defasagem / 60000).toFixed(1)} min`}
        </b>
      </div>
    </div>
  );
}
