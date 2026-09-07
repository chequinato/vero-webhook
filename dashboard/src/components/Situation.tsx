import { Odometer } from './Odometer';
import type { Stats } from '../types';

/**
 * 01 — SITUAÇÃO
 *
 * Hierarquia deliberadamente desigual: um número monumental à esquerda,
 * quatro leituras secundárias numa pilha ao centro, e a proporção do dia
 * como uma barra vertical à direita. Nada aqui é um cartão.
 *
 * Todos os números rolam como contador mecânico em vez de contarem de
 * zero: quando os dados voltam da API, só os dígitos que mudaram se
 * mexem — o olho pega a mudança sem varrer o painel inteiro.
 */

const brl = (v: number, decimals = 0) =>
  v.toLocaleString('pt-BR', { minimumFractionDigits: decimals, maximumFractionDigits: decimals });

export function Situation({ stats, loading }: { stats: Stats | null; loading: boolean }) {
  if (loading || !stats) return <SituationSkeleton />;

  const pct = (v: number) => (stats.total > 0 ? (v / stats.total) * 100 : 0);
  const interceptacao = pct(stats.bloqueadas + stats.suspeitas);

  const readouts = [
    { label: 'Aprovadas', value: stats.aprovadas, sub: `${pct(stats.aprovadas).toFixed(1)}% do fluxo`, tone: 'success' },
    { label: 'Bloqueadas', value: stats.bloqueadas, sub: `${pct(stats.bloqueadas).toFixed(1)}% do fluxo`, tone: 'danger' },
    { label: 'Suspeitas', value: stats.suspeitas, sub: `${pct(stats.suspeitas).toFixed(1)}% em revisão`, tone: 'warning' },
    { label: 'Em análise', value: stats.aceitasProvisoria, sub: 'aceitas provisoriamente', tone: 'info' },
  ] as const;

  const parts = [
    { key: 'APR', cls: 'verde', v: stats.aprovadas },
    { key: 'SUS', cls: 'amber', v: stats.suspeitas },
    { key: 'BLQ', cls: 'signal', v: stats.bloqueadas },
    { key: 'PND', cls: 'azul', v: stats.aceitasProvisoria },
  ];
  const somaPartes = parts.reduce((a, p) => a + p.v, 0) || 1;

  return (
    <section className="situation">
      {/* ── Número monumental ── */}
      <div className="hero">
        <div className="hero-figure">
          <span className="hero-num rv-cut" style={{ ['--i' as string]: 1 }}>
            <Odometer value={stats.total} duration={780} />
          </span>
          <span className="hero-sup rv" style={{ ['--i' as string]: 4 }}>
            transações
            <br />
            processadas
          </span>
        </div>

        <div className="hero-underline rv-rule" style={{ ['--i' as string]: 3 }} />

        <div className="hero-par">
          <p className="serif-note hero-note rv" style={{ ['--i' as string]: 5 }}>
            Volume acumulado de <b>R$ {brl(stats.valorTotal)}</b>, ticket médio de{' '}
            <b>R$ {brl(stats.valorMedio, 2)}</b>. O modelo atribui risco médio de{' '}
            <b>{(stats.riskScoreMedio * 100).toFixed(1)}%</b> à janela corrente.
          </p>

          {/* Leitura de interceptação: o número que um analista procura primeiro. */}
          <div className="hero-marca rv" style={{ ['--i' as string]: 6 }}>
            <span className="hero-marca-num">
              <Odometer value={interceptacao} decimals={1} suffix="%" />
            </span>
            <span className="hero-marca-lbl">
              interceptação
              <em>bloqueio + revisão</em>
            </span>
          </div>
        </div>
      </div>

      {/* ── Leituras secundárias ── */}
      <div className="stack">
        {readouts.map((r, i) => (
          <div className="readout rv" key={r.label} style={{ ['--i' as string]: 2 + i }}>
            <div className="readout-label">{r.label}</div>
            <div className="readout-val">
              <Odometer value={r.value} duration={640 + i * 60} />
            </div>
            <div className={`readout-sub readout-sub--${r.tone}`}>{r.sub}</div>
          </div>
        ))}
      </div>

      {/* ── Proporção ── */}
      <div className="ratio rv" style={{ ['--i' as string]: 6 }}>
        <div className="ratio-bar">
          {parts.map((p, i) => (
            <div
              key={p.key}
              className={`ratio-seg ratio-seg--${p.cls}`}
              style={{
                height: `${(p.v / somaPartes) * 100}%`,
                animationDelay: `${380 + i * 90}ms`,
                transition: 'height 320ms var(--e-out)',
              }}
              title={`${p.key} · ${p.v}`}
            />
          ))}
        </div>
        <div className="ratio-keys">
          {[...parts].reverse().map(p => (
            <div className="ratio-key" key={p.key}>
              {p.key}
              <b>{((p.v / somaPartes) * 100).toFixed(0)}%</b>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

function SituationSkeleton() {
  return (
    <section className="situation">
      <div className="hero">
        <span className="skl" style={{ width: '62%', height: 120 }} />
        <div className="hero-underline" style={{ background: 'var(--rule)' }} />
        <span className="skl" style={{ width: '80%', height: 14, marginTop: 8 }} />
      </div>
      <div className="stack">
        {[0, 1, 2, 3].map(i => (
          <div className="readout" key={i}>
            <div className="readout-label">
              <span className="skl" style={{ width: 74, height: 9 }} />
            </div>
            <div className="readout-val">
              <span className="skl" style={{ width: 62, height: 28 }} />
            </div>
          </div>
        ))}
      </div>
      <div className="ratio">
        <div className="ratio-bar">
          <span className="skl" style={{ width: '100%', height: '100%' }} />
        </div>
      </div>
    </section>
  );
}
